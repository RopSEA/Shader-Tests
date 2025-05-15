Shader "Unlit/Grass"
{
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _WindTex ("_WindTex", 2D) = "white" {}
        _TopColor ("TopColor", Color) = (0,1,0,1)
        _BotColor ("BotColor", Color) = (0,1,0,1)
        _AmbColor ("AmbColor", Color) = (0,1,0,1)
        _TipColor ("TipColor", Color) = (0,1,0,1)
        _WindStren ("WindStren", float) = 1
    }

    SubShader {
        Cull Off
        Zwrite On

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma target 4.5

            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"
            #include "../Resources/Random.cginc"

            struct VertexData 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f 
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float saturationLevel : TEXCOORD1;
            };

            struct GrassData
            {
                float4 position;
                float2 uv;
            };

            sampler2D _MainTex, _HeightMap;
            sampler2D _WindTex;
            float4 _TopColor, _BotColor, _AmbColor, _TipColor;
            float4 _MainTex_ST;
            StructuredBuffer<GrassData> positionBuffer;
            float _Rotation, _DisplacementStrength;
            
            float4 RotateAroundYInDegrees (float4 vertex, float degrees) 
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float4(mul(m, vertex.xz), vertex.yw).xzyw;
            }

  

            v2f vert (VertexData v, uint instanceID : SV_INSTANCEID) 
            {
                v2f o;
            
                float3 localPosition = RotateAroundYInDegrees(v.vertex, 0).xyz;

                float4 grassPosition = positionBuffer[instanceID].position;

                float idHash = randValue(abs(grassPosition.x * 10000 + grassPosition.y * 100 + grassPosition.z * 0.05f + 2));
                idHash = randValue(idHash * 100000);

                //float localWindVariance = min(max(0.4f, randValue(instanceID)), 0.75f);
                                   //v.uv = sin((v.uv.x + v.uv.y) + _Time);

                float4 worldUV = float4(positionBuffer[instanceID].uv, 0, 0);

                float f = 1.0;
                float amp = 0.5;
                float silly = (worldUV.x + worldUV.y);// + noise((v.vertex.x + v.vertex.y) * f) * amp;

                float swayVariance = lerp(0.8, 1.0, idHash);
                float movement = v.uv.y  * (tex2Dlod(_WindTex, worldUV).r);
                movement *= swayVariance;

                localPosition.xz += (sin(f *(silly + _Time)) * amp);

                localPosition.xz *= v.vertex;

                float4 worldPosition = float4(grassPosition.xyz + localPosition, 1.0f);
                worldPosition.y *= 1.0f + positionBuffer[instanceID].position.w * lerp(0.8f, 1.0f, idHash);

                o.vertex = UnityObjectToClipPos(worldPosition);

               // o.vertex.xz *= (sin(silly) + _Time);
                //localPosition.y *= v.uv.y * (0.5f + grassPosition.w)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.saturationLevel = 1.0 - ((positionBuffer[instanceID].position.w - 1.0f) / 1.5f);
                o.saturationLevel = max(o.saturationLevel, 0.5f);
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target 
            {
                fixed4 col =  _TopColor;
                clip(-(0.5 - col.a));

                float luminance = LinearRgbToLuminance(col);

                float4 amb = lerp(_AmbColor, 1.0, i.uv.y);
                float4 tip = lerp(0.0f, _TipColor, i.uv.y *  (1.0f));

                float4 color = lerp(_BotColor, _TopColor, i.uv.y * i.uv.y * i.uv.y);
               // col.r *= saturation;
                
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float ndotl = DotClamped(lightDir, normalize(float3(0, 1, 0)));

                col = color * ndotl * amb;
                
                return col;
            }

            ENDCG
        }
    }
}
