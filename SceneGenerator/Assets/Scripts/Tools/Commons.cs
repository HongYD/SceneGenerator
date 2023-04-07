using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Commons
{
    internal static float[,] GetPerlinNoiseHeightMap(int width, int height, float scale, int octaves, float persistance, float lacunarity)
    {
        float[,] heightMap = new float[width, height];

        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float amplitude = 1.0f;
                float frequency = 1.0f;
                float noiseHeight = 0;
                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = x / scale * frequency;
                    float sampleY = y / scale * frequency;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }
                if (noiseHeight > maxNoiseHeight)
                {
                    maxNoiseHeight = noiseHeight;
                }
                else if (noiseHeight < minNoiseHeight)
                {
                    minNoiseHeight = noiseHeight;
                }
                heightMap[x, y] = noiseHeight;
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heightMap[x, y] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, heightMap[x, y]);
            }
        }
        return heightMap;
    }
}
