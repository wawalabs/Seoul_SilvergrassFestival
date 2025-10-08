using System.Collections;
using UnityEngine;

public class RippleEffect : MonoBehaviour
{
    [Header("해상도")]
    public int textureSize = 1920;

    [Header("입력")]
    public RenderTexture objectsRT;       // 플레이어 렌더링 RT

    [Header("스폰 조건")]
    public float minStepMeters = 0.20f;   // 이 거리만큼 이동했을 때만 스폰
    Vector3 _lastEmitPos; bool _hasLast;

    [Header("전파(Shader: RippleShader)")]
    public Shader rippleShader;
    [Range(0.90f, 0.999f)] public float damping = 0.975f; // 감쇠
    [Range(0.5f, 5f)] public float waveSpeed = 1.0f;  // 속도
    [Range(0f, 1f)] public float dispersion = 0.0f; // 필요 시

    [Header("스폰(Shader: AddShader)")]
    public Shader addShader;
    [Tooltip("리플 스폰 주기(초). 값↑ = 덜 잔물결")]
    [Range(0.01f, 0.50f)] public float pulseInterval = 0.12f;
    [Tooltip("입력 강도 스케일")]
    [Range(0f, 2f)] public float impulseScale = 0.85f;
    [Tooltip("입력 블러 반경(셸로우)")]
    [Range(0f, 3f)] public float spawnBlur = 1.0f;

    [Header("출력 바인딩")]
    public Renderer targetRenderer;       // _RippleTex 할당 대상

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
            // 1) 스폰(주기 제한)
            if (Time.time >= nextPulseTime && objectsRT && addMat)
            {
                addMat.SetTexture("_ObjectsRT", objectsRT);
                addMat.SetTexture("_CurrentRT", currRT);
                addMat.SetFloat("_ImpulseScale", impulseScale); // Add.shader에 노출 권장
                addMat.SetFloat("_SpawnBlur", spawnBlur);       // 없으면 무시됨
                Graphics.Blit(null, tempRT, addMat);

                // swap(tempRT, currRT)
                var rt0 = tempRT; tempRT = currRT; currRT = rt0;
                nextPulseTime = Time.time + pulseInterval;
            }

            // 2) 전파(매 프레임)
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
            nextPulseTime = 0f;           // 다음 루프에서 즉시 스폰
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
