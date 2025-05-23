using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using static UnityEngine.InputManagerEntry;

public class Grass : MonoBehaviour
{
    public int Demensions = 100;
    public int Density = 1;

    public int chunkDensity = 1;
    public int numChunks = 1;

    public float displacementStrength = 200.0f;
    public Material grassMaterial;
    public Mesh grassMesh;
    public Mesh grassMeshLOD;
    public Texture heightMap;
    public Texture GrassMap;

    [Range(0, 1000.0f)]
    public float lodCutoff = 1000.0f;

    [Range(0, 1000.0f)]
    public float distanceCutoff = 1000.0f;

    public bool updateGrass;

    public ComputeShader initializeGrassShader , GrassfrustumCulling;
    //private ComputeBuffer grassDataBuffer;
    private ComputeBuffer voteBuffer, scanBuffer, groupSumArrayBuffer, scannedGroupSumBuffer;

    private Texture2D wind;
    private int numThreadGroups, numVoteThreadGroups, numGroupScanThreadGroups, numWindThreadGroups, numGrassInitThreadGroups;
    private int numInstances, chunkDimension;

    GrassChunk[] chunks;

    // Sending Mesh Data to GPU
    private uint[] args;
    private uint[] argsLOD;

    Bounds fieldBounds;


    private struct GrassData 
    {
        public Vector4 position;
        public Vector2 uv;
        float displacement;
    }

    private struct GrassChunk
    {
        public ComputeBuffer argsBuffer;
        public ComputeBuffer argsBufferLOD;
        public ComputeBuffer positionsBuffer;
        public ComputeBuffer culledPositionsBuffer;
        public Bounds bounds;
        public Material material;
    }

    void OnEnable() 
    {
        // Frustum Culling Calc
        numInstances =  Mathf.CeilToInt(Demensions / numChunks) * chunkDensity;
        chunkDimension = numInstances;
        numInstances *= numInstances;

        numThreadGroups = Mathf.CeilToInt(numInstances / 128.0f);
        if (numThreadGroups > 128)
        {
            int powerOfTwo = 128;
            while (powerOfTwo < numThreadGroups)
                powerOfTwo *= 2;

            numThreadGroups = powerOfTwo;
        }
        else
        {
            while (128 % numThreadGroups != 0)
                numThreadGroups++;
        }
        numVoteThreadGroups = Mathf.CeilToInt(numInstances / 128.0f);
        numGroupScanThreadGroups = Mathf.CeilToInt(numInstances / 1024.0f);


        voteBuffer = new ComputeBuffer(numInstances, 4);
        scanBuffer = new ComputeBuffer(numInstances, 4);
        groupSumArrayBuffer = new ComputeBuffer(numInstances, 4);
        scannedGroupSumBuffer = new ComputeBuffer(numInstances, 4);



        // Demensions *= Density;
        //grassDataBuffer = new ComputeBuffer(numInstances, UnsafeUtility.SizeOf(typeof(GrassData)));
        //culledPositionsBuffer = new ComputeBuffer(numInstances, UnsafeUtility.SizeOf(typeof(GrassData)));

        //argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        //argsBufferLOD = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
 
        // Set COMPUTE SHADER
        initializeGrassShader.SetInt("_Dimension", Demensions);
        initializeGrassShader.SetInt("_ChunkDimension", chunkDimension);
        initializeGrassShader.SetInt("_NumChunks", numChunks);
        initializeGrassShader.SetInt("_Scale", chunkDensity);
        initializeGrassShader.SetTexture(0, "_HeightMap", heightMap);
        initializeGrassShader.SetFloat("_DisplacementStrength", displacementStrength);
       // initializeGrassShader.SetVector("_objPos", transform.position);
        //initializeGrassShader.Dispatch(0, Mathf.CeilToInt(Demensions / 8.0f), Mathf.CeilToInt(Demensions / 8.0f), 1);

        wind = GenerateText();

        args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
        args[0] = (uint)grassMesh.GetIndexCount(0);
        args[1] = (uint)0;
        args[2] = (uint)grassMesh.GetIndexStart(0);
        args[3] = (uint)grassMesh.GetBaseVertex(0);

        argsLOD = new uint[5] { 0, 0, 0, 0, 0 };
        argsLOD[0] = (uint)grassMeshLOD.GetIndexCount(0);
        argsLOD[1] = (uint)0;
        argsLOD[2] = (uint)grassMeshLOD.GetIndexStart(0);
        argsLOD[3] = (uint)grassMeshLOD.GetBaseVertex(0);

        initializeChunks();

        fieldBounds = new Bounds(Vector3.zero, new Vector3(-Demensions, displacementStrength * 2, Demensions));


        // SET MATERIAL SHADER
        /*
        grassMaterial.SetBuffer("positionBuffer", grassDataBuffer);
        grassMaterial.SetFloat("_Rotation", 0.0f);
        grassMaterial.SetFloat("_DisplacementStrength", displacementStrength);
        grassMaterial.SetTexture("_WindTex", wind);
        */
    }


    void initializeChunks()
    {
        chunks = new GrassChunk[numChunks * numChunks];

        for (int x = 0; x < numChunks; ++x)
        {
            for (int y = 0; y < numChunks; ++y)
            {
                chunks[x + y * numChunks] = initializeGrassChunk(x, y);
            }
        }
    }

    GrassChunk initializeGrassChunk(int xOffset, int yOffset)
    {
        GrassChunk chunk = new GrassChunk();

        chunk.argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        chunk.argsBufferLOD = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

        chunk.argsBuffer.SetData(args);
        chunk.argsBufferLOD.SetData(argsLOD);

        chunk.positionsBuffer = new ComputeBuffer(numInstances, UnsafeUtility.SizeOf(typeof(GrassData)));
        chunk.culledPositionsBuffer = new ComputeBuffer(numInstances, UnsafeUtility.SizeOf(typeof(GrassData)));
        int chunkDim = Mathf.CeilToInt(Demensions / numChunks);

        Vector3 c = new Vector3(0.0f, 0.0f, 0.0f);

        c.y = 0.0f;
        c.x = -(chunkDim * 0.5f * numChunks) + chunkDim * xOffset;
        c.z = -(chunkDim * 0.5f * numChunks) + chunkDim * yOffset;
        c.x += chunkDim * 0.5f;
        c.z += chunkDim * 0.5f;

        chunk.bounds = new Bounds(c, new Vector3(-chunkDim, 10.0f, chunkDim));

        initializeGrassShader.SetInt("_XOffset", xOffset);
        initializeGrassShader.SetInt("_YOffset", yOffset);
        initializeGrassShader.SetBuffer(0, "_GrassDataBuffer", chunk.positionsBuffer);
        initializeGrassShader.Dispatch(0, Mathf.CeilToInt(Demensions / numChunks) * chunkDensity, Mathf.CeilToInt(Demensions / numChunks) * chunkDensity, 1);

        chunk.material = new Material(grassMaterial);
        chunk.material.SetBuffer("positionBuffer", chunk.culledPositionsBuffer);
        chunk.material.SetFloat("_DisplacementStrength", displacementStrength);
        chunk.material.SetTexture("_WindTex", wind);
        chunk.material.SetInt("_ChunkNum", xOffset + yOffset * numChunks);

        return chunk;
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

    void CullGrass(GrassChunk chunk, Matrix4x4 VP, bool noLOD)
    {
        //Reset Args
        if (noLOD)
            chunk.argsBuffer.SetData(args);
        else
            chunk.argsBufferLOD.SetData(argsLOD);

        // Vote
        GrassfrustumCulling.SetMatrix("MATRIX_VP", VP);
        GrassfrustumCulling.SetBuffer(0, "_GrassDataBuffer", chunk.positionsBuffer);
        GrassfrustumCulling.SetBuffer(0, "_VoteBuffer", voteBuffer);
        GrassfrustumCulling.SetVector("_CameraPosition", Camera.main.transform.position);
        GrassfrustumCulling.SetFloat("_Distance", distanceCutoff);
        GrassfrustumCulling.Dispatch(0, numVoteThreadGroups, 1, 1);

        // Scan Instances
        GrassfrustumCulling.SetBuffer(1, "_VoteBuffer", voteBuffer);
        GrassfrustumCulling.SetBuffer(1, "_ScanBuffer", scanBuffer);
        GrassfrustumCulling.SetBuffer(1, "_GroupSumArray", groupSumArrayBuffer);
        GrassfrustumCulling.Dispatch(1, numThreadGroups, 1, 1);

        // Scan Groups
        GrassfrustumCulling.SetInt("_NumOfGroups", numThreadGroups);
        GrassfrustumCulling.SetBuffer(2, "_GroupSumArrayIn", groupSumArrayBuffer);
        GrassfrustumCulling.SetBuffer(2, "_GroupSumArrayOut", scannedGroupSumBuffer);
        GrassfrustumCulling.Dispatch(2, numGroupScanThreadGroups, 1, 1);

        // Compact
        GrassfrustumCulling.SetBuffer(3, "_GrassDataBuffer", chunk.positionsBuffer);
        GrassfrustumCulling.SetBuffer(3, "_VoteBuffer", voteBuffer);
        GrassfrustumCulling.SetBuffer(3, "_ScanBuffer", scanBuffer);
        GrassfrustumCulling.SetBuffer(3, "_ArgsBuffer", noLOD ? chunk.argsBuffer : chunk.argsBufferLOD);
        GrassfrustumCulling.SetBuffer(3, "_CulledGrassOutputBuffer", chunk.culledPositionsBuffer);
        GrassfrustumCulling.SetBuffer(3, "_GroupSumArray", scannedGroupSumBuffer);
        GrassfrustumCulling.Dispatch(3, numThreadGroups, 1, 1);
    }

    void Update() 
    {
        Matrix4x4 Proj = Camera.main.projectionMatrix;
        Matrix4x4 View = Camera.main.transform.worldToLocalMatrix;
        Matrix4x4 ViewProj = Proj * View;


        for (int i = 0; i < numChunks * numChunks; ++i)
        {
            float dist = Vector3.Distance(Camera.main.transform.position, chunks[i].bounds.center);

            bool noLOD = dist < lodCutoff;

            CullGrass(chunks[i], ViewProj, noLOD);
            if (noLOD)
                Graphics.DrawMeshInstancedIndirect(grassMesh, 0, chunks[i].material, fieldBounds, chunks[i].argsBuffer);
            else
                Graphics.DrawMeshInstancedIndirect(grassMeshLOD, 0, chunks[i].material, fieldBounds, chunks[i].argsBufferLOD);
        }

        /*
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
        */
    }

    void OnDisable()
    {
        voteBuffer.Release();
        scanBuffer.Release();
        groupSumArrayBuffer.Release();
        scannedGroupSumBuffer.Release();
        //wind.Release();
        //wind = null;
        scannedGroupSumBuffer = null;
        voteBuffer = null;
        scanBuffer = null;
        groupSumArrayBuffer = null;


        for (int i = 0; i < numChunks * numChunks; ++i)
        {
            FreeChunk(chunks[i]);
        }

        chunks = null;
    }

    void FreeChunk(GrassChunk chunk)
    {
        chunk.positionsBuffer.Release();
        chunk.positionsBuffer = null;
        chunk.culledPositionsBuffer.Release();
        chunk.culledPositionsBuffer = null;
        chunk.argsBuffer.Release();
        chunk.argsBuffer = null;
        chunk.argsBufferLOD.Release();
        chunk.argsBufferLOD = null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if (chunks != null)
        {
            for (int i = 0; i < numChunks * numChunks; ++i)
            {
                Gizmos.DrawWireCube(chunks[i].bounds.center, chunks[i].bounds.size);
            }
        }
    }


}
