using UnityEngine;

public class GroundMaterialManager : MonoBehaviour
{
    [SerializeField]
    private Material concreteMaterial;
    [SerializeField]
    private Material grassMaterial;
    [SerializeField]
    private Material sandMaterial;
    [SerializeField]
    private Material stairMaterial;

    [Header("Ground Objects")]
    [SerializeField]
    private GameObject flatGround;
    [SerializeField]
    private GameObject stairsGroup;

    private Renderer flatGroundRenderer;
    private string currentMaterialType = "Concrete";

    void Start()
    {
        if (flatGround == null || stairsGroup == null)
        {
            Debug.LogError("GroundMaterialManager: Missing references!");
            return;
        }

        flatGroundRenderer = flatGround.GetComponent<Renderer>();
        
        if (flatGroundRenderer == null)
        {
            Debug.LogError("GroundMaterialManager: Flat ground has no Renderer!");
            return;
        }

        Debug.Log($"FlatGround position: {flatGround.transform.position}");
        Debug.Log($"StairsGroup position: {stairsGroup.transform.position}");
        
        // Log all stairs positions
        Transform[] allStairs = stairsGroup.GetComponentsInChildren<Transform>();
        foreach (Transform stair in allStairs)
        {
            if (stair != stairsGroup.transform)
                Debug.Log($"{stair.gameObject.name} position: {stair.position}");
        }

        // Start with flat ground visible
        flatGround.SetActive(true);
        stairsGroup.SetActive(false);

        SetMaterial("Concrete");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
            SetMaterial("Concrete");
        if (Input.GetKeyDown(KeyCode.G))
            SetMaterial("Grass");
        if (Input.GetKeyDown(KeyCode.S))
            SetMaterial("Sand");
        if (Input.GetKeyDown(KeyCode.T))
            SetMaterial("Stair");
    }

    public void SetMaterial(string materialType)
    {
        if (currentMaterialType == materialType)
            return;

        currentMaterialType = materialType;

        switch (materialType)
        {
            case "Concrete":
                SetFlatGroundActive(concreteMaterial, "🏢 CONCRETE");
                break;
            case "Grass":
                SetFlatGroundActive(grassMaterial, "🌿 GRASS");
                break;
            case "Sand":
                SetFlatGroundActive(sandMaterial, "🏜️ SAND");
                break;
            case "Stair":
                SetStairsActive();
                break;
        }
    }

    void SetFlatGroundActive(Material material, string name)
    {
        Debug.Log($"{name} - FlatGround Active: true, StairsGroup Active: false");
        flatGround.SetActive(true);
        flatGroundRenderer.material = material;
        stairsGroup.SetActive(false);
    }

    void SetStairsActive()
    {
        Debug.Log($"⬆️ STAIRS - FlatGround Active: false, StairsGroup Active: true");
        Debug.Log($"StairsGroup position: {stairsGroup.transform.position}");
        
        flatGround.SetActive(false);
        stairsGroup.SetActive(true);
        
        Renderer[] stairRenderers = stairsGroup.GetComponentsInChildren<Renderer>();
        Debug.Log($"Found {stairRenderers.Length} renderers in StairsGroup");
        
        foreach (Renderer renderer in stairRenderers)
        {
            Debug.Log($"Applying material to: {renderer.gameObject.name}");
            renderer.material = stairMaterial;
        }
    }

    public string GetCurrentMaterial()
    {
        return currentMaterialType;
    }
}