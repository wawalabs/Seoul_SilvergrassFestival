using UnityEngine;
using OscSimpl;
using UnityEngine.VFX;

public class PlayerPositionReceiverBackup : MonoBehaviour
{
    public OscIn oscIn;                  // OSC ������Ʈ
    public GameObject[] players;        // ������ ��ġ�� Player��
    public VisualEffect[] visualeffects;      // �� Player�� �����ϴ� VFX

    [Header("�÷��̾� �̵� ����")]
    public float minX = -5f;
    public float maxX = 5f;
    public float minZ = -5f;
    public float maxZ = 5f;

    void Start()
    {
        if (oscIn == null)
        {
            Debug.LogError("OscIn ������Ʈ�� �������� �ʾҽ��ϴ�.");
            enabled = false;
            return;
        }

        if (players.Length != visualeffects.Length)
        {
            Debug.LogError("players �迭�� visualeffects �迭�� ���̰� ��ġ���� �ʽ��ϴ�.");
            enabled = false;
            return;
        }

        // ���� �� ��� �÷��̾�� ������ ��Ȱ��ȭ
        for (int i = 0; i < players.Length; i++)
        {
            players[i].SetActive(false);
            if (visualeffects[i] != null) visualeffects[i].gameObject.SetActive(false);
        }

        // ���� ���� ����
        oscIn.MapInt("/Count", OnCountReceived);
        // ��ġ ������ �ϰ� �ʹٸ� ������ ���ܵα�
        oscIn.Map("/Position", OnPositionReceived);
    }

    // ���� ���� ���� �� ����
    void OnCountReceived(int count)
    {
        Debug.Log($"���ŵ� ���� ����: {count}");

        if (count > players.Length)
        {
            Debug.LogWarning($"���� ��({count})�� �÷��̾� ��({players.Length})���� �����ϴ�. �ʰ����� �����մϴ�.");
            count = players.Length;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (i < count)
            {
                players[i].SetActive(true);
                if (visualeffects[i] != null) visualeffects[i].gameObject.SetActive(true);
            }
            else
            {
                players[i].SetActive(false);
                if (visualeffects[i] != null) visualeffects[i].gameObject.SetActive(false);
            }
        }
    }

    // ��ġ ������ �ϰ� ���� ���� ���
    void OnPositionReceived(OscMessage message)
    {
        int valueCount = message.Count();
        int sensorCount = valueCount / 2;

        for (int i = 0; i < players.Length && i < sensorCount; i++)
        {
            float sensorX, sensorY;
            if (!message.TryGet(i * 2, out sensorX) || !message.TryGet(i * 2 + 1, out sensorY))
            {
                Debug.LogWarning($"���� {i} �� �б� ����");
                continue;
            }

            float playerX = Mathf.Lerp(minX, maxX, sensorX);
            float playerZ = Mathf.Lerp(minZ, maxZ, sensorY);

            players[i].transform.position = new Vector3(playerX, players[i].transform.position.y, playerZ);
        }
    }
}
