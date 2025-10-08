using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class VFX_Position : MonoBehaviour
{

    public VisualEffect vfxGraph;
    public GameObject character;
    Vector2 character_position;

    void Start()
    {
        //vfxGraph = GetComponent<VisualEffect>();
    }



    void Update()
    {
        if (vfxGraph == null || character == null) return;

        Vector2 characterPosition = new Vector2(character.transform.position.x, character.transform.position.z);
        vfxGraph.SetVector2("Vector_Position", characterPosition);
    }
}
