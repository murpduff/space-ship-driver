
struct SpherePointInfo{
    float t1;
    float t2;
    float submerged;
};

SpherePointInfo sphIntersect(float3 ro, float3 rd, float3 sph, float rad){
    SpherePointInfo OUT;

    OUT.t1 = -1.0;

    OUT.submerged = 0.0;

    float3 oc = ro - sph;
    float b = dot(oc, rd);
    float c = dot(oc, oc) - rad * rad;
    float h = b * b - c;
    
    if(h < 0.0) return OUT;

    float t1 = -b - sqrt(h);
    float t2 = -b + sqrt(h);
    
    if(t1 > 0.0){
        OUT.t1 = t1;
        OUT.t2 = t2;
        OUT.submerged = 0.0;
    }else{
        OUT.t1 = t2;
        OUT.t2 = 0.0;
        OUT.submerged = 1.0;
    }

    return OUT;
}