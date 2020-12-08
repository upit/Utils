Shader "UI/UISlidingStrip"
{
	Properties
	{
	    _MainTex ("Fallback texture", 2D) = "black" {}
	    _Size ("Size", Range(0, 1)) = 0.5
	    _Direction ("Direction", Range(-3.14, 3.14)) = 0
	    _Speed ("Sliding speed", Range(0, 10)) = 1
	    _Period ("Period", Range(2, 50)) = 1
	    
	    [KeywordEnum(simple, Use sprite, Render sprite)] _Type ("Type", Int) = 0
	    _MaskSmooth ("Mask Smooth", Range(0, 1)) = 0
	}

	SubShader
	{
		Tags{ "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"	}
		
		Cull Back
		Lighting Off
		ZWrite Off
		ZTest Off
		Blend SrcAlpha OneMinusSrcAlpha
		Pass
		{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma shader_feature _TYPE_SIMPLE _TYPE_USE_SPRITE _TYPE_RENDER_SPRITE
						
			struct appdata
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};
			
			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				
				// Если без маски то передаем координаты полоски в одной координате, если с маской, то еще +2 координаты для UV изображения.
			#if _TYPE_SIMPLE
				    half
			#else
				    half3
			#endif
				texcoord  : TEXCOORD0;
			};

            float _Direction;

			v2f vert(appdata i)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(i.vertex);
                o.color = i.color;
                o.texcoord.x = o.vertex.x*sin(_Direction) + o.vertex.y*cos(_Direction);
				//o.texcoord.x = i.texcoord.x*sin(_Direction) + i.texcoord.y*cos(_Direction);
				
			#if !_TYPE_SIMPLE
				o.texcoord.yz = i.texcoord;
			#endif
                
				return o;
			}

			float _Size;
			float _Speed;
			float _Period;
		
		#if !_TYPE_SIMPLE
		    float _MaskSmooth;
			sampler2D _MainTex;
//			fixed4 _TextureSampleAdd;
		#endif
			
			fixed4 frag(v2f i) : COLOR
			{
		        half x = i.texcoord.x;
			    half offset = frac(_Time[0]*_Speed)*_Period-_Period*0.5;
	            
	            fixed4 result; 
	        #if _TYPE_SIMPLE
    			result = step(offset,x) * step(x, offset+_Size);
    		#else
    		    fixed4 tex =  tex2D(_MainTex, i.texcoord.yz);
    			result = smoothstep(offset,offset+_MaskSmooth,x) * smoothstep(x, x+_MaskSmooth,offset+_Size) * tex.a;
    			
    			#if _TYPE_RENDER_SPRITE
    			result+=tex;
    			#endif
    			
    		#endif

    		    return result*i.color;
			}

		ENDCG
		}
	}
}
