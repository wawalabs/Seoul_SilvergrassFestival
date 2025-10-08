using System.Collections;
using UnityEngine;

public class RippleEffect : MonoBehaviour
{
    [Header("�ػ�")]
    public int textureSize = 1920;

    [Header("�Է�")]
    public RenderTexture objectsRT;       // �÷��̾� ������ RT

    [Header("���� ����")]
    public float minStepMeters = 0.20f;   // �� �Ÿ���ŭ �̵����� ���� ����
    Vector3 _lastEmitPos; bool _hasLast;

    [Header("����(Shader: RippleShader)")]
    public Shader rippleShader;
    [Range(0.90f, 0.999f)] public float damping = 0.975f; // ����
    [Range(0.5f, 5f)] public float waveSpeed = 1.0f;  // �ӵ�
    [Range(0f, 1f)] public float dispersion = 0.0f; // �ʿ� ��

    [Header("����(Shader: AddShader)")]
    public Shader addShader;
    [Tooltip("���� ���� �ֱ�(��). ���� = �� �ܹ���")]
    [Range(0.01f, 0.50f)] public float pulseInterval = 0.12f;
    [Tooltip("�Է� ���� ������")]
    [Range(0f, 2f)] public float impulseScale = 0.85f;
    [Tooltip("�Է� �� �ݰ�(�зο�)")]
    [Range(0f, 3f)] public float spawnBlur = 1.0f;

    [Header("��� ���ε�")]
    public Renderer targetRenderer;       // _RippleTex �Ҵ� ���

    RenderTexture currRT, prevRT, tempRT;
    Material rippleMat, addMat;
    float nextPulseTime;

    void Start()
    {
        currRT = AllocRT();
        prevRT = AllocRT();
        tempRT = AllocRT();

        rippleMat = new Material(rippleShader);
        addMat = new Material(addShader);

        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        if (targetRenderer) targetRenderer.material.SetTexture("_RippleTex", currRT);

        StartCoroutine(Run());
    }

    RenderTexture AllocRT()
    {
        var rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RHalf);
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    IEnumerator Run()
    {
        while (true)
        {
            // 1) ����(�ֱ� ����)
            if (Time.time >= nextPulseTime && objectsRT && addMat)
            {
                addMat.SetTexture("_ObjectsRT", objectsRT);
                addMat.SetTexture("_CurrentRT", currRT);
                addMat.SetFloat("_ImpulseScale", impulseScale); // Add.shader�� ���� ����
                addMat.SetFloat("_SpawnBlur", spawnBlur);       // ������ ���õ�
                Graphics.Blit(null, tempRT, addMat);

                // swap(tempRT, currRT)
                var rt0 = tempRT; tempRT = currRT; currRT = rt0;
                nextPulseTime = Time.time + pulseInterval;
            }

            // 2) ����(�� ������)
            if (rippleMat)
            {
                rippleMat.SetTexture("_PrevRT", prevRT);
                rippleMat.SetTexture("_CurrentRT", currRT);
                rippleMat.SetFloat("_Damping", damping);
                rippleMat.SetFloat("_WaveSpeed", waveSpeed);
                rippleMat.SetFloat("_Dispersion", dispersion);

                Graphics.Blit(null, tempRT, rippleMat);
                Graphics.Blit(tempRT, prevRT);

                // swap(prevRT, currRT)
                var rt = prevRT; prevRT = currRT; currRT = rt;
            }

            yield return null;
        }
    }

    public void NotifyEmitterPos(Vector3 worldPos)
    {
        if (!_hasLast) { _lastEmitPos = worldPos; _hasLast = true; return; }
        if ((worldPos - _lastEmitPos).sqrMagnitude >= minStepMeters * minStepMeters)
        {
            nextPulseTime = 0f;           // ���� �������� ��� ����
            _lastEmitPos = worldPos;
        }
    }

    void OnDestroy()
    {
        if (currRT) currRT.Release();
        if (prevRT) prevRT.Release();
        if (tempRT) tempRT.Release();
        if (rippleMat) Destroy(rippleMat);
        if (addMat) Destroy(addMat);
    }
}
