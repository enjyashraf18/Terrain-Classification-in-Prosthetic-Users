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
check `example.py` fe training folder

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

 
