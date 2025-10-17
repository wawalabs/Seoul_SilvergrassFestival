using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaskingPlaneControls : MonoBehaviour
{
    [Header("Target")]
    public Transform maskingPlane;      // MaskingPlane ������Ʈ(Transform)

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
        // �ε�
        float s = PlayerPrefs.GetFloat(KeyScale, defaultScale);
        float z = PlayerPrefs.GetFloat(KeyPosZ, defaultPosZ);

        // UI ����
        if (scaleSlider) { scaleSlider.minValue = minScale; scaleSlider.maxValue = maxScale; scaleSlider.value = s; }
        if (posZSlider) { posZSlider.minValue = minPosZ; posZSlider.maxValue = maxPosZ; posZSlider.value = z; }

        // ����
        ApplyScale(s);
        ApplyPosZ(z);

        // �̺�Ʈ ���ε�
        if (scaleSlider) scaleSlider.onValueChanged.AddListener(v => { ApplyScale(v); Save(); });
        if (posZSlider) posZSlider.onValueChanged.AddListener(v => { ApplyPosZ(v); Save(); });
    }

    void ApplyScale(float s)
    {
        if (!maskingPlane) return;
        var ls = maskingPlane.localScale;
        ls.x = s; ls.z = s;                  // X,Z ���� ������
        maskingPlane.localScale = ls;
        if (scaleLabel) scaleLabel.text = s.ToString("0.000");
    }

    void ApplyPosZ(float z)
    {
        if (!maskingPlane) return;
        var p = maskingPlane.localPosition;
        p.z = z;                             // Z ��ġ�� ����
        maskingPlane.localPosition = p;
        if (posZLabel) posZLabel.text = z.ToString("0.000");
    }

    void Save()
    {
        PlayerPrefs.SetFloat(KeyScale, scaleSlider ? scaleSlider.value : defaultScale);
        PlayerPrefs.SetFloat(KeyPosZ, posZSlider ? posZSlider.value : defaultPosZ);
        PlayerPrefs.Save();
    }

    // �ʿ��ϸ� UI ��ư�� ������ �⺻�� ����
    public void ResetToDefault()
    {
        if (scaleSlider) scaleSlider.value = defaultScale;
        if (posZSlider) posZSlider.value = defaultPosZ;
    }
}
