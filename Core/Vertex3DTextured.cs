﻿using System.Numerics;
using Veldrid;

namespace UAlbion.Core
{
    internal struct Vertex3DTextured
    {
        public float X;
        public float Y;
        public float Z;
        public float U;
        public float V;

        public Vertex3DTextured(Vector3 position, Vector2 textureCoordinates)
        {
            X = position.X;
            Y = position.Y;
            Z = position.Z;
            U = textureCoordinates.X;
            V = textureCoordinates.Y;
        }

        public Vertex3DTextured(float x, float y, float z, float u, float v) { X = x; Y = y; Z = z; U = u; V = v; }

        public static VertexLayoutDescription VertexLayout => new VertexLayoutDescription(
            VertexLayoutHelper.Vector3D("Position"),
            VertexLayoutHelper.Vector2D("TexCoords"));
    }
}