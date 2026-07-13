Shader "Custom/UnlitTextureOverlay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing // Добавляем поддержку инстансинга для Single Pass Instanced

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Добавляем для поддержки инстансинга
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO // Добавляем для стерео рендеринга в VR
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); // Настраиваем ID инстанса
                UNITY_INITIALIZE_OUTPUT(v2f, o); // Инициализируем вывод
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // Инициализируем стерео для VR

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); // Опционально, если потребуется в будущем для стерео-текстур

                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}