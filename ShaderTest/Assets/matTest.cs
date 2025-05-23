using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class matTest : MonoBehaviour
{

    public Transform objectT;
    public int num = 0;
    public Mesh mesh;

    private Vector3[] vert;


    // Start is called before the first frame update
    void OnEnable()
    {
        Matrix4x4 local = transform.localToWorldMatrix;

        Matrix4x4 objectMAT = objectT.localToWorldMatrix;

       // mesh = gameObject.GetComponent<Mesh>();
        vert = mesh.vertices;
        Vector2[] uvs = new Vector2[vert.Length];

        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vert[i].x, vert[i].z);
        }

        

        objectT.position = new Vector3( vert[num].x ,0,vert[num].y);
    }

    // Update is called once per frame
    void Update()
    {
        objectT.position = vert[num]; // new Vector3(vert[num].x, 0, vert[num].y);
        num++;

        if (num >= vert.Length)
        {
            num = 0;
        }
    }
}
