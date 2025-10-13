using UnityEngine;
using UnityEngine.UI;

public class MaskSwitcher : MonoBehaviour
{
    public Renderer maskingPlane;         // MaskingPlane�� MeshRenderer
    public Texture sharpTex, blurTex;     // �� ����ũ �ؽ�ó
    public Dropdown dropdown;             // Main UI�� Sharp/Blur ����

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
