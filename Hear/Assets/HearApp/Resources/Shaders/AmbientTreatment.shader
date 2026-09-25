// Cheap "atmospheric depth" treatment for the Home screen's full-bleed ambient world background:
// a small multi-tap blur (the real softness comes from blitting a pre-downsampled source into
// this pass - see WorldArt.GetTreatedAmbient) plus a darken and desaturate lerp, so the carousel
// cards and UI read as the sharp interactive foreground against a hazier backdrop. Human request
// 2026-09-25, relaying designer feedback ("uzivatel si nemusi uvedomit, ze tam je carousel").
// Not URP-pipeline-specific on purpose - Graphics.Blit works with a plain CG shader regardless of
// the active render pipeline, keeping this independent of any SRP include-path churn.
Shader "Hidden/HearAmbientTreatment"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DarkenAmount ("Darken Amount", Range(0,1)) = 0.32
        _DesaturateAmount ("Desaturate Amount", Range(0,1)) = 0.45
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _DarkenAmount;
            float _DesaturateAmount;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                fixed3 sum = fixed3(0, 0, 0);
                sum += tex2D(_MainTex, i.uv + texel * float2(-1, -1)).rgb;
                sum += tex2D(_MainTex, i.uv + texel * float2( 0, -1)).rgb;
                sum += tex2D(_MainTex, i.uv + texel * float2( 1, -1)).rgb;
                sum += tex2D(_MainTex, i.uv + texel * float2(-1,  0)).rgb;
                sum += tex2D(_MainTex, i.uv).rgb;
                sum += tex2D(_MainTex, i.uv + texel * float2( 1,  0)).rgb;
                sum += tex2D(_MainTex, i.uv + texel * float2(-1,  1)).rgb;
                sum += tex2D(_MainTex, i.uv + texel * float2( 0,  1)).rgb;
                sum += tex2D(_MainTex, i.uv + texel * float2( 1,  1)).rgb;
                fixed3 color = sum / 9.0;

                fixed luminance = dot(color, fixed3(0.299, 0.587, 0.114));
                color = lerp(color, fixed3(luminance, luminance, luminance), _DesaturateAmount);
                color = lerp(color, fixed3(0, 0, 0), _DarkenAmount);

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
