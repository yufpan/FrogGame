Shader "GameEffect/AllEffect01_code"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)]_CullMode("开启双面", Float) = 2
        [Enum(AddItive,1,AlphaBlend,10)]_dst("叠加模式", Float) = 10
        
        [HDR]_Tex01Color("第一层贴图颜色", Color) = (1,1,1,1)
        _Intensity("Intensity" , Float ) = 1 
        _Tex01("第一层贴图", 2D) = "white" {}
        

        _Texture_R_A("第一层贴图的A通道和R通道的切换", Range( 0 , 1)) = 0
        _Tex01Rotator("第一层贴图旋转", Range( 0 , 1)) = 0
        _TexSpeedU("第一层贴图流动U", Float) = 0
        _TexSpeedV("第一层贴图流动V", Float) = 0
        [Toggle(_USECUSTSPEED_ON)] _UseCustSpeed("开启粒子自定义", Float) = 0
        
        [Toggle(_MASK01_ON_ON)] _Mask01_ON("Mask01遮罩开关", Float) = 0
        [Header(Mask)]_Tex01Mask("第一层贴图遮罩", 2D) = "white" {}
        _Mask01SpeedU("第一层遮罩流动U", Float) = 0
        _Mask01SpeedV("第一层遮罩流动V", Float) = 0
        
        [Header(RaoDong)][Toggle(_USERAODONG1_ON)] _UseRaoDong1("开启扰动效果", Float) = 0
        _RaoDongTex("扰动图", 2D) = "white" {}
        _RaoDong("扰动强度", Range( 0 , 1)) = 0
        _RaoDongTexSpeedU("扰动流动U", Float) = 0
        _RaoDongTexSpeedV("扰动流动V", Float) = 0
        [Toggle(_USEDISSOLVE_ON)] _UseDissolve("开启溶解", Float) = 0
        _Dissolve("溶解贴图", 2D) = "white" {}
        _DissolveSpeedU1("溶解流动U", Float) = 0
        _DissolveSpeedV1("溶解流动V", Float) = 0
        _DissolveValue2("溶解强度", Range( 0 , 1)) = 0
        _SoftaDissolve1("软硬边强度", Range( 0 , 1)) = 0
        _DissolveWidth1("溶解沟边宽度", Range( 0 , 1)) = 0.1
        [HDR]_DissolveColor1("溶解沟边颜色", Color) = (0,0,0,0)
        [Toggle(_USEPARCUSTOM_ON)] _UseParCustom("开启使用粒子自定义2(X)", Float) = 0
        _DissolveRaoDong("溶解贴图受扰动的强度(需要开启扰动才起作用)", Float) = 0.2
        [Toggle(_USEDISSOLVEMASK_ON)] _UseDissolveMask("开启溶解遮罩", Float) = 0
        _DissolveMask("溶解贴图遮罩(开启上方按钮才起作用)", 2D) = "white" {}
        [HideInInspector]_Dissolve_ST("Dissolve", Vector) = (1,1,0,0) 
       
        _MinX("Min X", Float) = -1000
        _MaxX("Max X", Float) = 1000
        _MinY("Min Y", Float) = -2000
        _MaxY("Max Y", Float) = 2000
    }
    
    SubShader
    {
        
        
        Tags { "Queue"="Transparent" }
        LOD 100

        CGINCLUDE
        #pragma target 3.0
        ENDCG
        Blend SrcAlpha [_dst]
        AlphaToMask Off
        Cull [_CullMode]
        ColorMask RGBA
        ZWrite Off
        ZTest LEqual
        
        
        
        Pass
        {
           
            //Tags { "LightMode"="ForwardBase" }
            CGPROGRAM

            

            // #ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
            //     //only defining to not throw compilation error over Unity 5.5
            //     #define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
            // #endif
            #pragma vertex vert
            #pragma fragment frag
    
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "UnityShaderVariables.cginc"
            #define ASE_NEEDS_FRAG_COLOR
            #pragma shader_feature_local _USEDISSOLVE_ON
            #pragma shader_feature_local _USERAODONG1_ON
            #pragma shader_feature_local _USECUSTSPEED_ON
            #pragma shader_feature_local _USEDISSOLVEMASK_ON
            #pragma shader_feature_local _USEPARCUSTOM_ON
            #pragma shader_feature_local _MASK01_ON_ON

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 Texcoord : TEXCOORD0;
                float4 Texcoord1 : TEXCOORD1;
                
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                //#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
                    float3 worldPos : TEXCOORD0;
               // #endif
                float4 VertexColor : COLOR;
                float4 Texcoord1 : TEXCOORD1;
                float4 ase_texcoord2 : TEXCOORD2;
              
            };

             float _dst , _Intensity;
             float _CullMode;
             sampler2D _Tex01;
             float4 _Tex01_ST;
             float _Tex01Rotator;
             sampler2D _RaoDongTex;
             float _RaoDongTexSpeedU;
             float _RaoDongTexSpeedV;
             float4 _RaoDongTex_ST;
             float _RaoDong;
             float _TexSpeedU;
             float _TexSpeedV;
             float4 _Tex01Color;
             float _SoftaDissolve1;
             sampler2D _Dissolve;
             float _DissolveSpeedU1;
             float _DissolveSpeedV1;
             float4 _Dissolve_ST;
             float _DissolveRaoDong;
             sampler2D _DissolveMask;
             float4 _DissolveMask_ST;
             float _DissolveValue2;
             float _DissolveWidth1;
             float4 _DissolveColor1;
             float _Texture_R_A;
             sampler2D _Tex01Mask;
             float _Mask01SpeedU;
             float _Mask01SpeedV;
             float4 _Tex01Mask_ST;

            float _MinX;
            float _MaxX;
            float _MinY;
            float _MaxY;
            float4 _ClipRect;
            float _LimitEffectRange;

            v2f vert ( appdata v )
            {
                v2f o;      
                o.VertexColor = v.color;
                o.Texcoord1.xy = v.Texcoord.xy;
                o.ase_texcoord2 = v.Texcoord1;
                
                //setting value to unused interpolator channels and avoid initialization warnings
                o.Texcoord1.zw = 0;
                float3 vertexValue = float3(0, 0, 0);
                #if ASE_ABSOLUTE_VERTEX_POS
                    vertexValue = v.vertex.xyz;
                #endif
                vertexValue = vertexValue;
                #if ASE_ABSOLUTE_VERTEX_POS
                    v.vertex.xyz = vertexValue;
                #else
                    v.vertex.xyz += vertexValue;
                #endif
                o.vertex = UnityObjectToClipPos(v.vertex);

                //#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
                    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                //#endif
                return o;
            }
            
            fixed4 frag (v2f i ) : SV_Target
            {
             
                fixed4 finalColor;
                #ifdef ASE_NEEDS_FRAG_WORLD_POSITION
                    float3 WorldPosition = i.worldPos;
                #endif
                float2 uv_Tex01 = i.Texcoord1.xy * _Tex01_ST.xy + _Tex01_ST.zw;
                float cos12 = cos( ( ( _Tex01Rotator * UNITY_PI ) * 2.0 ) );
                float sin12 = sin( ( ( _Tex01Rotator * UNITY_PI ) * 2.0 ) );
                float2 rotator12 = mul( uv_Tex01 - float2( 0.5,0.5 ) , float2x2( cos12 , -sin12 , sin12 , cos12 )) + float2( 0.5,0.5 );
                float2 appendResult62 = (float2(_RaoDongTexSpeedU , _RaoDongTexSpeedV));
                float2 uv_RaoDongTex = i.Texcoord1.xy * _RaoDongTex_ST.xy + _RaoDongTex_ST.zw;
                float2 panner58 = ( 1.0 * _Time.y * appendResult62 + uv_RaoDongTex);
                float4 tex2DNode55 = tex2D( _RaoDongTex, panner58 );
                #ifdef _USERAODONG1_ON
                    float2 staticSwitch140 = ( rotator12 + ( tex2DNode55.r * _RaoDong ) );
                #else
                    float2 staticSwitch140 = rotator12;
                #endif
                float2 appendResult7 = (float2(_TexSpeedU , _TexSpeedV));
                float4 texCoord264 = i.ase_texcoord2;
                texCoord264.xy = i.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
                float2 appendResult265 = (float2(texCoord264.x , texCoord264.y));
                #ifdef _USECUSTSPEED_ON
                    float2 staticSwitch263 = appendResult265;
                #else
                    float2 staticSwitch263 = ( _Time.y * appendResult7 );
                #endif
                float4 tex2DNode1 = tex2D( _Tex01, ( staticSwitch140 + staticSwitch263 ) );
                float4 temp_output_86_0 = ( float4( (i.VertexColor).rgb , 0.0 ) * tex2DNode1 * _Tex01Color );
                float temp_output_93_0 = ( 1.0 - _SoftaDissolve1 );
                float2 appendResult119 = (float2(_DissolveSpeedU1 , _DissolveSpeedV1));
                float2 uv_Dissolve = i.Texcoord1.xy * _Dissolve_ST.xy + _Dissolve_ST.zw;
                float2 appendResult234 = (float2(_Dissolve_ST.z , _Dissolve_ST.w));
                float2 temp_output_236_0 = ( uv_Dissolve + appendResult234 );
                #ifdef _USERAODONG1_ON
                    float2 staticSwitch149 = ( temp_output_236_0 + ( tex2DNode55.r * _DissolveRaoDong ) );
                #else
                    float2 staticSwitch149 = temp_output_236_0;
                #endif
                float2 panner90 = ( 1.0 * _Time.y * appendResult119 + staticSwitch149);
                float4 tex2DNode91 = tex2D( _Dissolve, panner90 );
                float2 uv_DissolveMask = i.Texcoord1.xy * _DissolveMask_ST.xy + _DissolveMask_ST.zw;
                #ifdef _USEDISSOLVEMASK_ON
                    float staticSwitch135 = ( tex2DNode91.r * tex2D( _DissolveMask, uv_DissolveMask ).r );
                #else
                    float staticSwitch135 = tex2DNode91.r;
                #endif
                #ifdef _USEPARCUSTOM_ON
                    float staticSwitch259 = texCoord264.z;
                #else
                    float staticSwitch259 = _DissolveValue2;
                #endif
                float temp_output_97_0 = ( ( staticSwitch135 + 1.0 ) - ( staticSwitch259 * 2.0 ) );
                float smoothstepResult99 = smoothstep( 0.0 , temp_output_93_0 , temp_output_97_0);
                float smoothstepResult98 = smoothstep( 0.0 , ( temp_output_93_0 + _DissolveWidth1 ) , temp_output_97_0);
                #ifdef _USEDISSOLVE_ON
                    float4 staticSwitch146 = ( ( ( smoothstepResult99 - smoothstepResult98 ) * _DissolveColor1 ) + temp_output_86_0 );
                #else
                    float4 staticSwitch146 = temp_output_86_0;
                #endif
                float lerpResult278 = lerp( tex2DNode1.r , tex2DNode1.a , _Texture_R_A);
                float2 appendResult24 = (float2(_Mask01SpeedU , _Mask01SpeedV));
                float2 uv_Tex01Mask = i.Texcoord1.xy * _Tex01Mask_ST.xy + _Tex01Mask_ST.zw;
                float2 panner18 = ( 1.0 * _Time.y * appendResult24 + uv_Tex01Mask);
                #ifdef _MASK01_ON_ON
                    float staticSwitch276 = tex2D( _Tex01Mask, panner18 ).r;
                #else
                    float staticSwitch276 = 1.0;
                #endif
                #ifdef _USEDISSOLVE_ON
                    float staticSwitch147 = smoothstepResult99;
                #else
                    float staticSwitch147 = 1.0;
                #endif
                float4 appendResult272 = (float4(staticSwitch146.rgb , ( lerpResult278 * staticSwitch276 * staticSwitch147 * i.VertexColor.a * _Tex01Color.a )));
                
                
                finalColor = saturate(appendResult272 *_Intensity);

                bool inArea = i.worldPos.x >= _MinX && i.worldPos.x <= _MaxX && i.worldPos.y >= _MinY && i.worldPos.y <= _MaxY;
                 
                return inArea ? finalColor : fixed4(0, 0, 0, 0);

                //return finalColor;
            }
            ENDCG
        }
    }
    
}
