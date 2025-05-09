#define LIGHT

#include "UnityCG.cginc"
#include "Lighting.cginc"


float4 Light()
{
    return _LightColor0;
}
