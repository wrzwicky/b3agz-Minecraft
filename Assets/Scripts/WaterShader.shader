Shader "Minecraft/Water Shader" {

    Properties {
        _MainTex("First Texture", 2D) = "white" {}
        _SecondTex("Second Texture", 2D) = "white" {}
    }

    SubShader {

        Tags {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }
        LOD 100
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {

            CGPROGRAM
                #pragma vertex vertFunction
                #pragma fragment fragFunction
                #pragma target 2.0

                #include "UnityCG.cginc"

                struct appdata {

                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color: COLOR;

                };

                struct v2f {

                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;

                };

                sampler2D _MainTex;
                sampler2D _SecondTex;

                float GlobalLightLevel;
                float minGlobalLightLevel;
                float maxGlobalLightLevel;

                v2f vertFunction(appdata v) {

                    v2f o;

                    o.vertex = UnityObjectToClipPos(v.vertex);  //transform from world to screen
                    o.uv = v.uv;
                    o.color = v.color;

                    return o;

                }

                // i is from vertFunction above
                fixed4 fragFunction(v2f i) : SV_Target {

                    i.uv.x += (_SinTime.x * 0.5);

                    fixed4 tex1 = tex2D(_MainTex, i.uv);
                    fixed4 tex2 = tex2D(_SecondTex, i.uv);

                    fixed4 color = lerp(tex1, tex2, 0.5 + (_SinTime.w * 0.5));

                    float light = (maxGlobalLightLevel - minGlobalLightLevel) * GlobalLightLevel + minGlobalLightLevel;
                    light *= i.color.a;
                    light = clamp(light, minGlobalLightLevel, maxGlobalLightLevel);

                    color = lerp(float4(0, 0, 0, 1), color, light);

                    color.a = 0.5f;

                    return color;

                }

                ENDCG

        }
    }
}
