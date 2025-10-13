using UnityEngine;
using TMPro;   // 추가
using UnityEngine.UI;

public class MaskSwitcher : MonoBehaviour
{
    public Renderer maskingPlane;
    public Texture sharpTex, blurTex;
    public TMP_Dropdown dropdown;    // TMP용 드롭다운으로 변경

    const string Key = "MaskType";

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
