Shader "Unlit/Water"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WaterCol ("TopColor", Color) = (0,0,1,1)
    }
    SubShader
    {
        Cull Off
        Zwrite On
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "../Resources/Random.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _WaterCol;

            v2f vert (appdata v)
            {
                v2f o;

                float L, S;

                float amp, waveL, waveSpeed;
                float4 Direction;


                L = 6;
                S = 100;

                amp = 1;
                waveL = 2/L;
                waveSpeed = waveL * S;


                float waveOne =  amp * sin(1 * (v.vertex.xy) *  (waveL) +  (_Time) * waveSpeed);
                v.vertex.y += waveOne;

                o.vertex = UnityObjectToClipPos(v.vertex);

               // o.vertex.y = waveOne;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col * _WaterCol;
            }
            ENDCG
        }
    }
}
