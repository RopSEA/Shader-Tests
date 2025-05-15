using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using static UnityEngine.InputManagerEntry;

public class Grass : MonoBehaviour
{
    public int Demensions = 100;
    public int Density = 1;
    public float displacementStrength = 200.0f;
    public Material grassMaterial;
    public Mesh grassMesh;
    public Texture heightMap;

    public bool updateGrass;

    public ComputeShader initializeGrassShader;
    private ComputeBuffer grassDataBuffer, argsBuffer;

    private Texture2D wind;
    private int numWindThreadGroups;
    private struct GrassData 
    {
        public Vector4 position;
        public Vector2 uv;
    }

    void Start() 
    {
        Demensions *= Density;
        grassDataBuffer = new ComputeBuffer(Demensions * Demensions, 4 * 7);
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        updateGrassBuffer();
    }

    void updateGrassBuffer() 
    {
        // Set COMPUTE SHADER
        initializeGrassShader.SetInt("_Dimension", Demensions);
        initializeGrassShader.SetInt("_Scale", Density);
        initializeGrassShader.SetBuffer(0, "_GrassDataBuffer", grassDataBuffer);
        initializeGrassShader.SetTexture(0, "_HeightMap", heightMap);
        initializeGrassShader.SetFloat("_DisplacementStrength", displacementStrength);
        initializeGrassShader.Dispatch(0, Mathf.CeilToInt(Demensions / 8.0f), Mathf.CeilToInt(Demensions / 8.0f), 1);

        wind = GenerateText();

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
        args[0] = (uint)grassMesh.GetIndexCount(0);
        args[1] = (uint)grassDataBuffer.count;
        args[2] = (uint)grassMesh.GetIndexStart(0);
        args[3] = (uint)grassMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);


        // SET MATERIAL SHADER
        grassMaterial.SetBuffer("positionBuffer", grassDataBuffer);
        grassMaterial.SetFloat("_Rotation", 0.0f);
        grassMaterial.SetFloat("_DisplacementStrength", displacementStrength);
        grassMaterial.SetTexture("_WindTex", wind);
    }


    Texture2D GenerateText()
    {
        int height = 256;
        int width = 256;

        Texture2D texture = new Texture2D(width, height);

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                Color color = CalcColor(i, j, width, height);
                texture.SetPixel(i, j, color);
            }
        }

        texture.Apply();
        return texture;
    }

    Color CalcColor(int x, int y, int width, int height)
    {
        float xCoord = (float)x / width * 20;
        float yCoord = (float)y / height * 20;

        float sample = Mathf.PerlinNoise(x, y);
        return new Color(sample, sample, sample);
    }

    void Update() 
    {
        // SET MATERIAL SHADER
        grassMaterial.SetBuffer("positionBuffer", grassDataBuffer);
        grassMaterial.SetFloat("_Rotation", 0.0f);
        grassMaterial.SetFloat("_DisplacementStrength", displacementStrength);
        // INSTANCE ALL MESH AT ONCE
        Graphics.DrawMeshInstancedIndirect(grassMesh, 0, grassMaterial, new Bounds(Vector3.zero, new Vector3(-500.0f, 200.0f, 500.0f)), argsBuffer);

        if (updateGrass) {
            updateGrassBuffer();
            updateGrass = false;
        }
    }
    
    void OnDisable() 
    {
        grassDataBuffer.Release();
        argsBuffer.Release();
        grassDataBuffer = null;
        argsBuffer = null;
    }
}
