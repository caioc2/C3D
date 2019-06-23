// Standard geometry shader example
// https://github.com/keijiro/StandardGeometryShader

#include "UnityCG.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardUtils.cginc"

#pragma enable_d3d11_debug_symbols

// Cube map shadow caster; Used to render point light shadows on platforms
// that lacks depth cube map support.
#if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
#define PASS_CUBE_SHADOWCASTER
#endif

// Shader uniforms
half4 _Color;
sampler2D _MainTex;
float4 _MainTex_ST;

half _Glossiness;
half _Metallic;

sampler2D _BumpMap;
float _BumpScale;

sampler2D _OcclusionMap;
float _OcclusionStrength;

float _LocalTime;
float _extStrenght;
int _uid;

StructuredBuffer<float3> _vertices;
StructuredBuffer<float2> _uv;
StructuredBuffer<int> _triangles;

float4x4 _obj2World;


struct VOut
{
	float4 p0 : POSITION0;
	float4 p1 : POSITION1;
	float4 p2 : POSITION2;
	float2 uv0 : TEXCOORD0;
	float2 uv1 : TEXCOORD1;
};

// Fragment varyings
struct Varyings
{
    float4 position : SV_POSITION;

#if defined(PASS_CUBE_SHADOWCASTER)
    // Cube map shadow caster pass
    float3 shadow : TEXCOORD0;

#elif defined(UNITY_PASS_SHADOWCASTER)
    // Default shadow caster pass

#else
    // GBuffer construction pass
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
    float4 tspace0 : TEXCOORD1;
    float4 tspace1 : TEXCOORD2;
    float4 tspace2 : TEXCOORD3;
    half3 ambient : TEXCOORD4;

#endif
};

//
// Vertex stage
//

VOut Vertex(uint uid : SV_VertexID)
{
	//Load the points and uv to construct the surface
	uint3 tri_idx = _triangles[uid];
	
	VOut o;
	o.p0 = mul(_obj2World, float4(_vertices[tri_idx.x], 1.0f));
	o.p1 = mul(_obj2World, float4(_vertices[tri_idx.x+1], 1.0f));
	o.p2 = mul(_obj2World, float4(_vertices[tri_idx.x+2], 1.0f));
	o.uv0 = _uv[tri_idx.x];
	o.uv1 = _uv[tri_idx.x+1];
    return o;
}

//
// Geometry stage
//

Varyings VertexOutput(float3 wpos, half3 wnrm, half4 wtan, float2 uv)
{
    Varyings o;

#if defined(PASS_CUBE_SHADOWCASTER)
    // Cube map shadow caster pass: Transfer the shadow vector.
    o.position = UnityWorldToClipPos(float4(wpos, 1));
    o.shadow = wpos - _LightPositionRange.xyz;

#elif defined(UNITY_PASS_SHADOWCASTER)
    // Default shadow caster pass: Apply the shadow bias.
    float scos = dot(wnrm, normalize(UnityWorldSpaceLightDir(wpos)));
    wpos -= wnrm * unity_LightShadowBias.z * sqrt(1 - scos * scos);
    o.position = UnityApplyLinearShadowBias(UnityWorldToClipPos(float4(wpos, 1)));

#else
    // GBuffer construction pass
    half3 bi = cross(wnrm, wtan) * wtan.w * unity_WorldTransformParams.w;
    o.position = UnityWorldToClipPos(float4(wpos, 1));
    o.normal = wnrm;
    o.texcoord = uv;
    o.tspace0 = float4(wtan.x, bi.x, wnrm.x, wpos.x);
    o.tspace1 = float4(wtan.y, bi.y, wnrm.y, wpos.y);
    o.tspace2 = float4(wtan.z, bi.z, wnrm.z, wpos.z);
    o.ambient = ShadeSHPerVertex(wnrm, 0);

#endif
    return o;
}

float3 ConstructNormal(float3 v1, float3 v2, float3 v3)
{
    return normalize(cross(v2 - v1, v3 - v1));
}

///https://math.stackexchange.com/questions/180418/calculate-rotation-matrix-to-align-vector-a-to-vector-b-in-3d

//return the rotation matrix which transforms vector a(0,1,0) onto b, assumes a and b normalized

//A quaternion float4 (q) represents rotation of θ degrees around a given axis (x,y,z)
//Applying this transformation on a point p is \Phi_q(p) = qpq* = cosθ p + (1 − cosθ)dot(n, p) n + sinθ cross(n, p).
//With n = normalize(cross(p,q))
//Exapanding and simplifying \Phi_q(p) =  (I + sinθ [n] + sin²θ (1 - cos θ) [n]²)p
//                                     =  (I + [cross(a,b)] + 1 /(dot(a,b) + 1) [cross(a,b)]²)p
// With [x] the skew-symetric cross product matrix of x.
float3x3 rotationMatrix(float3 b)
{
	static const float eps = 1e-10;
	static const float3 a = float3(0.0f, 1.0f, 0.0f);
	float3 v = cross(a, b);
	float c = dot(a, b);

	static const float3x3 id = { 1.0f, 0.0f, 0.0f,
					0.0f, 1.0f, 0.0f,
					0.0f, 0.0f, 1.0f
	};

	float3x3 vm = { 0.0f, -v.z,  v.y,
		             v.z, 0.0f, -v.x,
		            -v.y,  v.x, 0.0f

	};

	vm = id + vm + mul(vm, vm) / (1 + c + eps);
	return vm;
}

#define nc (3)
#define PI (3.14159265f)


void transformCircle(float3 p[nc], float3 pos, float scale, float3 normal, out float3 ret[nc]) {
	float3x3 rot = rotationMatrix(normal);
	for (uint i = 0; i < nc; ++i) {
		ret[i] = mul(rot, p[i] * scale) + pos;
	}
}
[maxvertexcount(nc*2+2)]
void Geometry(
    point VOut input[1],
    inout TriangleStream<Varyings> outStream
)
{	
	
	//Circle discretized in nc points
	static float3 c[nc];
	for (uint i = 0; i < nc; ++i) {
		float t = 2.0f*PI*i / (nc*1.0f);
		c[i] = float3(cos(t), 0.0f, sin(t));
	}


	float3 p0 = input[0].p0.xyz;
	float3 p1 = input[0].p1.xyz;
	float3 p2 = input[0].p2.xyz;

	half3 n0 = normalize(half3(p1 - p0));
	half3 n1 = normalize(half3(p2 - p1));
	//tree diameter at node i stored in the texcoord.x
	float s0 = input[0].uv0.x;
	float s1 = input[0].uv1.x;

	float3 a[nc], b[nc];

	transformCircle(c, p0, s0, n0, a);
	transformCircle(c, p1, s1, n1, b);

	//Triangle Strip  2  4  2i 2
	//                |\ |\ |\ |
	//                | \| \| \|
	//                1  3 2i+1 1
	for (uint i = 0; i < nc - 1; ++i) {
		outStream.Append(VertexOutput(a[i], normalize(half3(a[i] - p0)), half4(normalize(a[i+1] - a[i]), 1.0f), float2(i / (nc*1.0f), input[0].uv0.y)));
		outStream.Append(VertexOutput(b[i], normalize(half3(b[i] - p1)), half4(normalize(b[i+1] - b[i]), 1.0f), float2(i / (nc*1.0f), input[0].uv1.y)));
	}
	outStream.Append(VertexOutput(a[nc-1], normalize(half3(a[nc - 1] - p0)), half4(normalize(a[0] - a[nc - 1]), 1.0f), float2((nc - 1.0f) / (nc*1.0f), input[0].uv0.y)));
	outStream.Append(VertexOutput(b[nc - 1], normalize(half3(b[nc - 1] - p1)), half4(normalize(b[0] - b[nc - 1]), 1.0f), float2((nc-1.0f) / (nc*1.0f), input[0].uv1.y)));
	outStream.Append(VertexOutput(a[0], normalize(half3(a[0] - p0)), half4(normalize(a[1] - a[0]), 1.0f), float2(0.0f / (nc*1.0f), input[0].uv0.y)));
	outStream.Append(VertexOutput(b[0], normalize(half3(b[0] - p1)), half4(normalize(b[1] - b[0]), 1.0f), float2(0.0f / (nc*1.0f), input[0].uv1.y)));
	outStream.RestartStrip();
	
}

//
// Fragment phase
//

#if defined(PASS_CUBE_SHADOWCASTER)

// Cube map shadow caster pass
half4 Fragment(Varyings input) : SV_Target
{
    float depth = length(input.shadow) + unity_LightShadowBias.x;
    return UnityEncodeCubeShadowDepth(depth * _LightPositionRange.w);
}

#elif defined(UNITY_PASS_SHADOWCASTER)

// Default shadow caster pass
half4 Fragment() : SV_Target { return 0; }

#else

// GBuffer construction pass
void Fragment(
    Varyings input,
    out half4 outGBuffer0 : SV_Target0,
    out half4 outGBuffer1 : SV_Target1,
    out half4 outGBuffer2 : SV_Target2,
    out half4 outEmission : SV_Target3
)
{
    // Sample textures
	half3 albedo = tex2D(_MainTex, input.texcoord).rgb * _Color.rgb;

    half4 normal = tex2D(_BumpMap, input.texcoord);
    normal.xyz = UnpackScaleNormal(normal, _BumpScale);

    half occ = tex2D(_OcclusionMap, input.texcoord).g;
    occ = LerpOneTo(occ, _OcclusionStrength);

    // PBS workflow conversion (metallic -> specular)
    half3 c_diff, c_spec;
    half refl10;
    c_diff = DiffuseAndSpecularFromMetallic(
        albedo, _Metallic, // input
        c_spec, refl10     // output
    );

    // Tangent space conversion (tangent space normal -> world space normal)
	float3 wn =  normalize(float3(
        dot(input.tspace0.xyz, normal),
        dot(input.tspace1.xyz, normal),
        dot(input.tspace2.xyz, normal)
    ));

    // Update the GBuffer.
    UnityStandardData data;
    data.diffuseColor = c_diff;
    data.occlusion = occ;
    data.specularColor = c_spec;
    data.smoothness = _Glossiness;
    data.normalWorld = wn;
    UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

    // Calculate ambient lighting and output to the emission buffer.
    float3 wp = float3(input.tspace0.w, input.tspace1.w, input.tspace2.w);
    half3 sh = ShadeSHPerPixel(data.normalWorld, input.ambient, wp);
    outEmission = half4(sh * c_diff, 1) * occ;
}

#endif
