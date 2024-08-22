#ifndef INCLUDED_TEXARRAYS
#define INCLUDED_TEXARRAYS

#define UNITY_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)
#define UNITY_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex

#endif //ifndef INCLUDED_TEXARRAYS