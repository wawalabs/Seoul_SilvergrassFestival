using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ControlPanelUI : MonoBehaviour
{
    [Header("Targets")]
    public PlayerPositionReceiverStable receiver; // ���� OSC/Ŭ������ ��ũ��Ʈ
    public MaskSwitcher maskSwitcher;             // MaskingPlane�� ���� ��ũ��Ʈ

    [Header("UI")]
    public TMP_InputField clusterMinIF;
    public TMP_InputField clusterRadiusIF;
    public TMP_InputField maxClustersIF;
    public TMP_Dropdown maskDropdown; // 0: Sharp, 1: Blur

    const string K_Min = "ClusterMin";
    const string K_Rad = "ClusterRadius";
    const string K_Max = "MaxClusters";
    const string K_Mask = "MaskType";

    void Awake()
    {
        // ���尪 �ε�
        int min = PlayerPrefs.GetInt(K_Min, 3);
        float rad = PlayerPrefs.GetFloat(K_Rad, 3f);
        int max = PlayerPrefs.GetInt(K_Max, 10);
        int mask = PlayerPrefs.GetInt(K_Mask, 0);

        // UI �ݿ�
        clusterMinIF.text = min.ToString();
        clusterRadiusIF.text = rad.ToString("0.00");
        maxClustersIF.text = max.ToString();
        maskDropdown.value = mask;

        // ��Ÿ�� ����
        ApplyCluster();
        ApplyMask(mask);

        // �̺�Ʈ ����
        clusterMinIF.onEndEdit.AddListener(_ => ApplyCluster());
        clusterRadiusIF.onEndEdit.AddListener(_ => ApplyCluster());
        maxClustersIF.onEndEdit.AddListener(_ => ApplyCluster());
        maskDropdown.onValueChanged.AddListener(ApplyMask);
    }

    void ApplyCluster()
    {
        if (!receiver) return;

        // �Ľ�
        int min = SafeInt(clusterMinIF.text, 3);
        float rad = SafeFloat(clusterRadiusIF.text, 3f);
        int max = SafeInt(maxClustersIF.text, 10);

        // Ŭ����
        min = Mathf.Max(1, min);
        rad = Mathf.Clamp(rad, 0.1f, 5f);
        max = Mathf.Clamp(max, 1, 64);

        // ����
        receiver.clusterMinMembers = min;
        receiver.clusterRadius = rad;
        receiver.maxClusters = max;

        // ����
        PlayerPrefs.SetInt(K_Min, min);
        PlayerPrefs.SetFloat(K_Rad, rad);
        PlayerPrefs.SetInt(K_Max, max);
        PlayerPrefs.Save();
    }

    void ApplyMask(int idx)
    {
        if (maskSwitcher) maskSwitcher.SendMessage("Apply", idx, SendMessageOptions.DontRequireReceiver);
        PlayerPrefs.SetInt(K_Mask, idx);
        PlayerPrefs.Save();
    }

    static int SafeInt(string s, int d) { return int.TryParse(s, out var v) ? v : d; }
    static float SafeFloat(string s, float d) { return float.TryParse(s, out var v) ? v : d; }
}
