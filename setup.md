# Unity ↔ Python Socket Setup Guide

This guide explains how to connect a Python server with a Unity project using TCP sockets.

---

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

### Step 1: Create Folder Structure

```
Assets/
└── Scripts/
    └── Networking/
```

### Step 2: Add `SocketClient.cs`

Create a file named `SocketClient.cs` and paste:

```csharp
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;
    public static System.Action<PredictionData> OnPredictionReceived;

    TcpClient client;
    NetworkStream stream;
    Thread receiveThread;

    public PredictionData latestPrediction; 
    string latestRawMessage;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ConnectToServer();
    }

    void ConnectToServer()
    {
        client = new TcpClient("127.0.0.1", 5050);
        stream = client.GetStream();

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveData()
    {
        byte[] buffer = new byte[1024];

        while (true)
        {
            int length = stream.Read(buffer, 0, buffer.Length);
            if (length != 0)
            {
                latestRawMessage = Encoding.UTF8.GetString(buffer, 0, length);
            }
        }
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(latestRawMessage))
        {
            Debug.Log("Raw: " + latestRawMessage);

            latestPrediction = JsonUtility.FromJson<PredictionData>(latestRawMessage);

            OnPredictionReceived?.Invoke(latestPrediction);

            Debug.Log("Predicted: " + latestPrediction.predicted);
            Debug.Log("Actual: " + latestPrediction.actual);
            Debug.Log("Confidence: " + latestPrediction.confidence);

            latestRawMessage = null;
        }
    }
}
```

---

### Step 3: Add `PredictionData.cs`

Create a file named `PredictionData.cs`:

```csharp
[System.Serializable]
public class PredictionData
{
    public string predicted;
    public string actual;
    public float confidence;
}
```

---

### Step 4: Add to Scene

* Create an empty GameObject in Unity
* Attach the `SocketClient` script to it by choosing Add component in the Inspector Panel

---

## 🔄 4. Using the Data in Other Scripts
to use the data in other scripts you need to paste this code in your files and you can access the data easily !

```csharp
void OnEnable()
{
    SocketClient.OnPredictionReceived += HandlePrediction;
}

void OnDisable()
{
    SocketClient.OnPredictionReceived -= HandlePrediction;
}

void HandlePrediction(PredictionData data)
{
    Debug.Log("From another script → " + data.predicted);
    Debug.Log("Confidence: " + data.confidence);
}
```

---

## 🚀 5. Run Everything

1. Start the Python server: run this in terminal

```
python app.py
```

2. Press **Run** in Unity

---

##  You're Ready!

Your Unity application should now be receiving real-time predictions from the Python server 🎉
