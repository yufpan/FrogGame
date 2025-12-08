Shader "Custom/FrogWarning"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WarningColor ("Warning Color", Color) = (1,1,0,1)
        _FlashSpeed ("Flash Speed", Float) = 5.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _WarningColor;
            float _FlashSpeed;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // 计算闪烁效果：使用sin函数实现周期性闪烁
                // sin值从-1到1，转换为0到1的范围，形成一个完整的闪烁周期（变亮再变暗）
                float flash = (sin(_Time.y * _FlashSpeed) + 1.0) * 0.5;
                
                // 将警告颜色以浅浅的高光形式叠加到原色上
                // 使用较低的混合强度，让高光效果更柔和
                fixed3 highlightColor = _WarningColor.rgb * _WarningColor.a * flash * 0.6; // 0.3是叠加强度，可以调整
                fixed3 finalColor = c.rgb + highlightColor;
                
                c.rgb = finalColor;
                return c;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}

