using UnityEngine;
public class MultiDisplayInit : MonoBehaviour
{
    void Start()
    {
        for (int i = 1; i < Display.displays.Length; i++)
            Display.displays[i].Activate(); // 2¹øºÎÅÍ ÄÒ´Ù
    }
}
