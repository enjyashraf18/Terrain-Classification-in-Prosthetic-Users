import numpy as np
import pandas as pd
import xgboost as xgb
import msvcrt
import os
import glob
from pathlib import Path
import pickle, json, socket, time
from scipy.signal import butter, filtfilt
from scipy.stats import skew, kurtosis, iqr
from scipy.fft import rfft, rfftfreq 
from collections import Counter
#============= state ===============
terrain_state = {
    "actual": None,
    "send_count": 0,
    "skip": False
}
# =========== Chunk Looping ======================
current_dir = Path(__file__).parent
data_folder = current_dir / "dataverse_files" / "P17"
all_files = sorted(list(data_folder.glob("*.csv")))

def chunk_list(lst, chunk_size=4):
    for i in range(0, len(lst), chunk_size):
        yield lst[i:i + chunk_size]
        
# ================== LOAD Model ==================
clf = xgb.XGBClassifier()
clf.load_model(current_dir / "Model" / "NG" / "Training" / "terrain_model.ubj")

with open(current_dir /"Model"/"NG"/"Training"/ "preprocessor.pkl", "rb") as f:
    prep = pickle.load(f)

with open(current_dir /"Model"/"NG"/ "json" / "model_meta.json") as f:
    meta = json.load(f)

# ================== META ==================
C = meta["constants"]
FS = C["FS"]
LOWPASS_CUTOFF = C["LOWPASS_CUTOFF"]
FILTER_ORDER = C["FILTER_ORDER"]
MIN_STRIDE = C["MIN_STRIDE_SAMPLES"]
MAX_STRIDE = C["MAX_STRIDE_SAMPLES"]

ALL_SENSORS = meta["all_sensors"]
SIGNAL_COLUMNS = meta["signal_columns"]
OPTIONAL_COLS = meta["optional_cols"]
FEATURE_SIGNALS = meta["feature_signals"]
FEAT_COLS = meta["feature_columns"]

_b, _a = butter(FILTER_ORDER, LOWPASS_CUTOFF / (FS / 2.0), btype="low")

# ================== HELPERS ==================
def hampel_filter(x, half_window=5, sigma=3.0):
    x = x.copy().astype(float)
    for i in range(len(x)):
        lo, hi = max(0, i - half_window), min(len(x), i + half_window + 1)
        w = x[lo:hi]
        med = np.median(w)
        mad = np.median(np.abs(w - med))
        if mad > 0 and abs(x[i] - med) > sigma * 1.4826 * mad:
            x[i] = med
    return x

def spectral_entropy(x):
    p = np.abs(rfft(x - x.mean())) ** 2
    p = p[1:]
    total = p.sum()
    if total <= 0: return 0.0
    p /= total
    return float(-(p * np.log(p + 1e-12)).sum())

def dominant_freq(x):
    centered = x - x.mean()
    s = np.abs(rfft(centered))
    if len(s) <= 1: return 0.0
    return float(rfftfreq(len(centered), 1.0 / FS)[int(np.argmax(s[1:]) + 1)])

def freq_band_energy(x, lo, hi):
    centered = x - x.mean()
    power = np.abs(rfft(centered)) ** 2
    freqs = rfftfreq(len(centered), 1.0 / FS)
    total = power.sum()
    if total <= 0: return 0.0
    return float(power[(freqs >= lo) & (freqs < hi)].sum() / total)

def summarize_signal(values, prefix):
    values = np.asarray(values, dtype=float)
    if values.size == 0 or not np.isfinite(values).any():
        return {f"{prefix}__{n}": np.nan for n in (
            "mean", "std", "min", "max", "median", "iqr", "rms", "energy",
            "skew", "kurtosis", "p10", "p90", "range", "start", "end", "delta",
            "dom_freq", "spec_entropy", "band_0_2", "band_2_5", "band_5_20")}
    std_v = float(np.std(values))
    return {
        f"{prefix}__mean": float(np.mean(values)),
        f"{prefix}__std": std_v,
        f"{prefix}__min": float(np.min(values)),
        f"{prefix}__max": float(np.max(values)),
        f"{prefix}__median": float(np.median(values)),
        f"{prefix}__iqr": float(iqr(values)),
        f"{prefix}__rms": float(np.sqrt(np.mean(values ** 2))),
        f"{prefix}__energy": float(np.mean(values ** 2)),
        f"{prefix}__skew": float(skew(values, bias=False)) if (len(values) > 2 and std_v > 1e-9) else 0.0,
        f"{prefix}__kurtosis": float(kurtosis(values, bias=False)) if (len(values) > 3 and std_v > 1e-9) else 0.0,
        f"{prefix}__p10": float(np.percentile(values, 10)),
        f"{prefix}__p90": float(np.percentile(values, 90)),
        f"{prefix}__range": float(np.max(values) - np.min(values)),
        f"{prefix}__start": float(values[0]),
        f"{prefix}__end": float(values[-1]),
        f"{prefix}__delta": float(values[-1] - values[0]),
        f"{prefix}__dom_freq": dominant_freq(values),
        f"{prefix}__spec_entropy": spectral_entropy(values),
        f"{prefix}__band_0_2": freq_band_energy(values, 0.0, 2.0),
        f"{prefix}__band_2_5": freq_band_energy(values, 2.0, 5.0),
        f"{prefix}__band_5_20": freq_band_energy(values, 5.0, 20.0),
    }

def load_sensor_csv(csv_path):
    df = pd.read_csv(csv_path)
    cont_cols = [c for c in SIGNAL_COLUMNS + OPTIONAL_COLS if c in df.columns]
    for col in cont_cols:
        df[col] = pd.to_numeric(df[col], errors="coerce")
        if df[col].isna().any():
            df[col] = df[col].interpolate(limit_direction="both").ffill().bfill()
    if len(df) >= 3 * FILTER_ORDER and cont_cols:
        for col in cont_cols:
            v = df[col].to_numpy(dtype=float)
            if np.isfinite(v).all() and np.std(v) > 1e-9:
                df[col] = filtfilt(_b, _a, v)
    for col in cont_cols:
        df[col] = hampel_filter(df[col].to_numpy())
    for prefix, axes in [
        ("Acc", ("Acc_X", "Acc_Y", "Acc_Z")),
        ("FreeAcc", ("FreeAcc_E", "FreeAcc_N", "FreeAcc_U")),
        ("Gyr", ("Gyr_X", "Gyr_Y", "Gyr_Z")),
        ("Mag", ("Mag_X", "Mag_Y", "Mag_Z")),
        ("VelInc", ("VelInc_X", "VelInc_Y", "VelInc_Z")),
    ]:
        if all(a in df.columns for a in axes):
            df[f"{prefix}_MAG"] = np.sqrt((df[list(axes)].to_numpy(dtype=float) ** 2).sum(axis=1))
    return df 


def build_stride_features(sensor_frames, stride_idx):
    n = len(stride_idx)
    feats = {"stride_samples": n, "stride_duration_s": n / FS, "stride_freq_hz": FS / n if n else np.nan}
    for sensor in ALL_SENSORS:
        frame = sensor_frames.get(sensor)
        for sig in FEATURE_SIGNALS:
            key = f"{sensor}__{sig}"
            if frame is None or sig not in frame.columns:
                feats.update(summarize_signal(np.array([]), key));
                continue
            valid = stride_idx[(stride_idx >= 0) & (stride_idx < len(frame))]
            vals = frame[sig].iloc[valid].to_numpy(dtype=float) if len(valid) else np.array([])
            feats.update(summarize_signal(vals, key))
    return feats

def match_terrain_With_unity(label: str) -> str:
    mapping = {
        "flat": "Concrete",
        "slope ascent": "Stair Up",
        "slope descent": "Stair Down",
        "stair ascent": "Stairs",
        "stair descent": "Stairs",
        "grass": "Grass",   
        "gravel": "Sand",
        "uneven terrain": "Sand"
    }

    normalized = label.strip().lower()
    return mapping.get(normalized, label)

# ================== STREAM FUNCTION ==================
def predict_and_stream(paths, client):
    frames = {s: load_sensor_csv(Path(p)) for s, p in paths.items()}
    min_len = min(len(df) for df in frames.values())
    frames = {s: df.iloc[:min_len].reset_index(drop=True) for s, df in frames.items()}

    ref = frames["PS"]
    ref["Steps"] = pd.to_numeric(ref["Steps"], errors="coerce")

    valid = ref.loc[ref["Steps"].notna() & (ref["Steps"] > 0)]

    id_to_label = {int(k): v for k, v in meta["label_encoder"].items()}
    sorted_ids = sorted(id_to_label.keys())

    buffer_preds = []
    buffer_confs = []

    # across patches
    global terrain_state
    if "terrain_state" not in globals():
        terrain_state = {
            "actual": None,
            "send_count": 0,
            "skip": False
        }

    def flush_buffer():
        if not buffer_preds:
            return

        counts = Counter(buffer_preds)
        majority_label, _ = counts.most_common(1)[0]

        selected_confs = [
            c for p, c in zip(buffer_preds, buffer_confs)
            if p == majority_label
        ]
        avg_conf = float(np.mean(selected_confs)) if selected_confs else 0.0

        message = {
            "predicted": str(majority_label),
            "actual": str(terrain_state["actual"]),
            "confidence": avg_conf
        }

        client.sendall((json.dumps(message) + "\n").encode())
        print("Sent:", message)

    # ================= main loop of steps  =================
    for step_id, g in valid.groupby("Steps"):
        idx = g.index.to_numpy(dtype=int)

        actual_id = int(g["Terrain"].iloc[0])
        actual_label = id_to_label.get(actual_id, "UNKNOWN")
        actual_label_unity = match_terrain_With_unity(actual_label)

        # ===== init =====
        if terrain_state["actual"] is None:
            terrain_state["actual"] = actual_label_unity

        # ===== terrain change =====
        if actual_label_unity != terrain_state["actual"]:
            flush_buffer()

            buffer_preds.clear()
            buffer_confs.clear()

            terrain_state["actual"] = actual_label_unity
            terrain_state["send_count"] = 0
            terrain_state["skip"] = False

        # ===== skip inference completely =====
        if terrain_state["skip"]:
            continue

        # ===== stride validity =====
        if not (MIN_STRIDE <= len(idx) <= MAX_STRIDE):
            continue

        # ===== inference =====
        feats = build_stride_features(frames, idx)
        row = pd.DataFrame([feats])[FEAT_COLS]

        X = prep.transform(row)
        enc = int(clf.predict(X)[0])

        label = id_to_label.get(sorted_ids[enc], "UNKNOWN")
        unity_label = match_terrain_With_unity(label)

        proba = clf.predict_proba(X)[0]
        conf = float(proba[enc])

        # ===== buffer =====
        buffer_preds.append(unity_label)
        buffer_confs.append(conf)

        # ===== send every 3 strides =====
        if len(buffer_preds) == 3:
            flush_buffer()

            buffer_preds.clear()
            buffer_confs.clear()

            terrain_state["send_count"] += 1

            # after 2 sends → lock (skip mode)
            if terrain_state["send_count"] >= 2:
                terrain_state["skip"] = True
                print(f"[INFO] Skip mode activated for terrain: {terrain_state['actual']}")

        time.sleep(feats["stride_duration_s"])
# ================= STREAM LOOP ===================        
def run_stream_loop():
    print("Starting stream... Press Q to stop")

    while True:
        for batch in chunk_list(all_files, 4):

            # check stop key
            if msvcrt.kbhit():
                key = msvcrt.getch().decode("utf-8").lower()
                if key == 'q':
                    print("Stopping loop...")
                    return

            # build paths dict dynamically
            paths = {
                "PS": batch[0],
                "TH": batch[1] if len(batch) > 1 else batch[0],
                "TR": batch[2] if len(batch) > 2 else batch[0],
                "OS": batch[3] if len(batch) > 3 else batch[0],
            }

            print("Processing batch:", [p.name for p in batch])

            predict_and_stream(paths, client)

# ================== MAIN SERVER ==================
if __name__ == "__main__":
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(("0.0.0.0", 5050))
    server.listen(1)

    print("Waiting for Unity...")
    client, addr = server.accept()
    print("Connected:", addr)

    run_stream_loop()

    client.close()
    server.close()  
