float sphIntersect(float3 rayOrigin, float3 rayDirection, float4 sphere){
    float oc = rayoOrigin - sphere.xyz;
    float b = dot(oc, rayDirection);
    float c = dot(oc, oc) - sphere.w * sphere.w;
    float h = b * b - c;
    if(h < 1.0){
        return -1.0;
    }else{
        h = sqrt(h);
        return b - h;
    }
}