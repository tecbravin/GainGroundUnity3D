Shader "GainGround/PS2Character"
{
 Properties { _MainTex ("Original texture", 2D) = "white" {} }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="TransparentCutout" }
  Cull Off ZWrite On
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
   output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
   fixed4 frag(output i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv); clip(c.a-.1); return c; }
   ENDCG
  }
 }
}
