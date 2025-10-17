using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaskingPlaneControls : MonoBehaviour
{
    [Header("Target")]
    public Transform maskingPlane;      // MaskingPlane 오브젝트(Transform)

    [Header("UI")]
    public Slider scaleSlider;          // 1.7~1.9
    public TMP_Text scaleLabel;
    public Slider posZSlider;           // 0.2~0.4
    public TMP_Text posZLabel;

    [Header("Ranges / Defaults")]
    public float minScale = 1.7f, maxScale = 1.9f, defaultScale = 1.8f;
    public float minPosZ = 0.2f, maxPosZ = 0.4f, defaultPosZ = 0.3f;

    const string KeyScale = "MaskScaleXZ";
    const string KeyPosZ = "MaskPosZ";

    void Awake()
    {
        // 로드
        float s = PlayerPrefs.GetFloat(KeyScale, defaultScale);
        float z = PlayerPrefs.GetFloat(KeyPosZ, defaultPosZ);

        // UI 설정
        if (scaleSlider) { scaleSlider.minValue = minScale; scaleSlider.maxValue = maxScale; scaleSlider.value = s; }
        if (posZSlider) { posZSlider.minValue = minPosZ; posZSlider.maxValue = maxPosZ; posZSlider.value = z; }

        // 적용
        ApplyScale(s);
        ApplyPosZ(z);

        // 이벤트 바인딩
        if (scaleSlider) scaleSlider.onValueChanged.AddListener(v => { ApplyScale(v); Save(); });
        if (posZSlider) posZSlider.onValueChanged.AddListener(v => { ApplyPosZ(v); Save(); });
    }

    void ApplyScale(float s)
    {
        if (!maskingPlane) return;
        var ls = maskingPlane.localScale;
        ls.x = s; ls.z = s;                  // X,Z 동시 스케일
        maskingPlane.localScale = ls;
        if (scaleLabel) scaleLabel.text = s.ToString("0.000");
    }

    void ApplyPosZ(float z)
    {
        if (!maskingPlane) return;
        var p = maskingPlane.localPosition;
        p.z = z;                             // Z 위치만 조정
        maskingPlane.localPosition = p;
        if (posZLabel) posZLabel.text = z.ToString("0.000");
    }

    void Save()
    {
        PlayerPrefs.SetFloat(KeyScale, scaleSlider ? scaleSlider.value : defaultScale);
        PlayerPrefs.SetFloat(KeyPosZ, posZSlider ? posZSlider.value : defaultPosZ);
        PlayerPrefs.Save();
    }

    // 필요하면 UI 버튼에 연결해 기본값 복원
    public void ResetToDefault()
    {
        if (scaleSlider) scaleSlider.value = defaultScale;
        if (posZSlider) posZSlider.value = defaultPosZ;
    }
}
