# Terrain-Classification-in-Prosthetic-Users

## 📖 Project Overview
This project is a **simulation of a lower-limb prosthetic system** integrated with Unity. It demonstrates how real-time sensor data can be used to classify terrain and adapt prosthetic behavior dynamically.

The system uses data from **four IMUs (Inertial Measurement Units)** placed on:
- Thigh  
- Trunk  
- Shank  
- Reference (normal baseline)
 <img width="873" height="601" alt="Image" src="https://github.com/user-attachments/assets/c05a78b0-08fc-42a8-bbfe-be381650b89f" /> 

These inputs are processed by a Python-based model that:
1. **Classifies the terrain**
   <img width="1211" height="318" alt="Image" src="https://github.com/user-attachments/assets/fe056bd3-f5c8-4863-8a33-eaf9a0a943b0" /> 
3. **Adjusts prosthetic stiffness in real time** based on the detected terrain  

The Unity environment visualizes this interaction, creating a responsive and adaptive prosthetic simulation.

---
## Demo :
https://github.com/user-attachments/assets/16ab9bd1-cb81-47be-8115-adfec7e14732

## 📦 1. Download Dataset

Download the dataset from the following link:
https://dataverse.no/dataset.xhtml?persistentId=doi%3A10.18710%2FU8RGDL

* Extract the downloaded files
* Place them inside:
  `<ProjectRoot>/dataverse_files/`

---

## 🐍 2. Setup Python Server

Install the required dependencies:

```
pip install -r requirements.txt
```

Run the server:

```
python app.py
```

---

## 🎮 3. Unity Setup

 Press **Run** in Unity

---

##  You're Ready!

Your Unity application should now be receiving real-time predictions from the Python server 🎉

