using UnityEngine;
using UnityEngine.UI;

public class MaskSwitcher : MonoBehaviour
{
    public Renderer maskingPlane;         // MaskingPlane의 MeshRenderer
    public Texture sharpTex, blurTex;     // 두 마스크 텍스처
    public Dropdown dropdown;             // Main UI의 Sharp/Blur 선택

    const string Key = "MaskType";        // 0: Sharp, 1: Blur

    void Start()
    {
        int type = PlayerPrefs.GetInt(Key, 0);
        Apply(type);
        if (dropdown)
        {
            dropdown.value = type;
            dropdown.onValueChanged.AddListener(Apply);
        }
    }

    void Apply(int type)
    {
        var mat = maskingPlane.sharedMaterial;
        mat.mainTexture = (type == 0) ? sharpTex : blurTex;
        PlayerPrefs.SetInt(Key, type);
        PlayerPrefs.Save();
    }
}
