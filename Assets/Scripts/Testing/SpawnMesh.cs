using System.Collections;
using System.Collections.Generic;
using Opencraft.Terrain.Blocks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class SpawnMesh : MonoBehaviour
{
    public Material material;
    // Start is called before the first frame update
    void Start()
    {
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        float[] array1 = new float[] { 1.0f,1.0f, 1.0f,1.0f, 1.0f,1.0f, 1.0f,1.0f};
        material.SetFloatArray("_uvSizes", array1);
        //float[] array2 = new float[] { 0.0f,0.0f, 0.0f};
        //material.SetFloatArray("_chunkOffset", array2);


        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];
        NativeArray<VertexAttributeDescriptor> _vertexLayout;
        _vertexLayout = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Temp);
        _vertexLayout[0] = new VertexAttributeDescriptor(attribute: VertexAttribute.Position, format: VertexAttributeFormat.UInt32, dimension: 1, stream: 0);
        
        int currentVertexBufferSize = 32;
        meshData.SetVertexBufferParams(currentVertexBufferSize, _vertexLayout);
        int currentIndexBufferSize = 64;
        meshData.SetIndexBufferParams(currentIndexBufferSize, IndexFormat.UInt16);
        NativeArray<uint> vertexBuffer = meshData.GetVertexData<uint>();
        NativeArray<ushort> indices = meshData.GetIndexData<ushort>();

        int numFaces = 0;
        uint length = 2;
        // Create a quad and write it directly to the buffer
        uint i = 0;
        uint j = 0;
        uint k = 0;
        AppendQuad(ref vertexBuffer, ref indices, numFaces,
            new uint3(i, j, k),
            new uint3(i, length + j, k),
            new uint3(i, length + j, k+1),
            new uint3(i, j, k+1),
            (int)FaceTypeShifted.xn, BlockType.Dirt);
        numFaces++;
        
        AppendQuad(ref vertexBuffer, ref indices, numFaces,
            new uint3(i, j, k+1),
            new uint3(i, length + j, k+1),
            new uint3(i+1, length + j, k+1),
            new uint3(i+1, j, k+1),
            (int)FaceTypeShifted.zp, BlockType.Stone);
        numFaces++;
        
        AppendQuad(ref vertexBuffer, ref indices, numFaces,
            new uint3(i, j+length, k),
            new uint3( i+1, j+length, k),
            new uint3( i+1, j+length, k+1),
            new uint3(i, j+length, k+1),
            (int)FaceTypeShifted.yp, BlockType.Gem);
        numFaces++;
        
        meshData.SetVertexBufferParams(numFaces * 4, _vertexLayout);
        meshData.SetIndexBufferParams(numFaces * 6, IndexFormat.UInt16);
        // Finalize the mesh
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, numFaces * 6), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        meshFilter.mesh = mesh;
    }
    
    private enum FaceTypeShifted : int
    {
        yp = 0 << 29,
        yn = 1 << 29,
        xp = 2 << 29,
        xn = 3 << 29,
        zp = 4 << 29,
        zn = 5 << 29,
    }
    
    private void AppendQuad(ref NativeArray<uint> verts, ref NativeArray<ushort> indices,
        int numFaces, uint3 bl, uint3 tl, uint3 tr, uint3 br, int normal, BlockType blockType)
    {
        int vb = numFaces * 4;
        // Pre bit-shifted texture ID map
        int texture = BlockData.BlockToTexture[(int)blockType];
        // 32 bit vertex layout:
        // byte: x pos
        // byte: y pos
        // byte: z pos
        // 5 bits: texture unit
        // 3 bits: normal
        uint shared = (uint)(texture | normal);
        // Top left vertex
        uint tlv = CombinePosition(tl, shared);

        // Top right vertex
        uint trv = CombinePosition(tr, shared);

        // Bottom left vertex
        uint blv = CombinePosition(bl, shared);

        // Bottom right vertex
        uint brv = CombinePosition(br, shared);
        
        // Store each vertex directly into the buffer
        verts[vb] = blv;
        verts[vb + 1] = brv;
        verts[vb + 2] = trv;
        verts[vb + 3] = tlv;
        
        // Set indices
        int ib = numFaces * 6;
        indices[ib] = (ushort)vb;
        indices[ib + 1] = (ushort)(vb + 1);
        indices[ib + 2] = (ushort)(vb + 2);
        indices[ib + 3] = (ushort)(vb + 2);
        indices[ib + 4] = (ushort)(vb + 3);
        indices[ib + 5] = (ushort)(vb + 0);
    }

    // Combine position data with the shared uint
    private uint CombinePosition(uint3 pos, uint shared)
    {
        return (uint)(shared | 
                      ((uint)pos.x & 255) |
                      ((uint)pos.y & 255) << 8 |
                      ((uint)pos.z & 255) << 16);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
