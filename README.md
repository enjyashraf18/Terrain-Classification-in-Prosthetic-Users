# Terrain-Classification-in-Prosthetic-Users
## Dataset used : 
Dataset: IMU dataset of lower limb prosthetic users traversing real-world terrain with and without a walking aid
https://dataverse.no/dataset.xhtml?persistentId=doi%3A10.18710%2FU8RGDL&version=&q=&fileTypeGroupFacet=%22Archive%22&fileAccess=Public&fileSortField=type&tagPresort=false


## Model Guide
| File | What it is |
|---|---|
| `terrain_model.ubj` | XGBoost booster weights (binary / cross-platform) |
| `preprocessor.pkl` | fitted sklearn Pipeline (median imputer + RobustScaler) |
| `model_meta.json` | Feature names, label map, and signal constants |


#### Installation
 
```bash
pip install xgboost scikit-learn scipy numpy pandas
```

### Example 
```python
import pickle, json
import xgboost as xgb
import pandas as pd
 
# load the three files
clf = xgb.XGBClassifier()
clf.load_model("terrain_model.ubj")
 
with open("preprocessor.pkl", "rb") as f:
    prep = pickle.load(f)
 
with open("model_meta.json") as f:
    meta = json.load(f)

# ely ba3doo
 
# select features in the exact order the model expects
# NOTE --> DataFrame must have all cols listed in meta["feature_columns"]
X_raw = stride_df[meta["feature_columns"]] (malsan y3ni)

# ely ba3doo

# preprocess + predict
X = prep.transform(X_raw)
encoded_preds = clf.predict(X)
 
# decoding
label_map = meta["label_encoder"]          # {"0": "Flat", "1": "Grass", ...}
terrain_names = [label_map[str(p)] for p in encoded_preds]
```

### Notes for decoding
```
TERRAIN_LABELS = {
    1: "Flat",
    2: "Grass",
    4: "Stair Ascent",
    5: "Stair Descent",
    6: "Slope Ascent",
    7: "Slope Descent",
    8: "Gravel",
    9: "Uneven Terrain",
}
```

1- fl training, we merged stairs (ascent and descent)

2- Unity only have concrete / sand / grass / stairs

so my guess is issss 
1. flat is concrete
2. sand is Uneven Terrain (gravel wouldnt be realistic sa7?)
 
