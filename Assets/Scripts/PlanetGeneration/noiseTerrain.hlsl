#include "noiseSimplex.cginc"

uint HashSeed(int seed)
{
    uint state = (uint)seed * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

float3 GetOffset(int seed){
    uint h = HashSeed(seed);

    float3 offset = float3(
        (float)(h % 1000u), 
        (float)((h * 1103515245u) % 1000u), 
        (float)((h * 1431655765u) % 1000u)
    );
    return offset;
}

float GetHeight(float3 pos, float frequency, float amplitude, int octaves, int seed, bool saturate){
    float freq = frequency;
    float amp = amplitude;
    float val = 0;

    for(int i = 0; i < octaves; i++){
        float3 offset = GetOffset(seed + 10 + i);
        val += snoise((pos + offset) * freq) * amp;
        freq *= 2;
        amp /= 2;
    }

    if(saturate){
        return (val + 1.0) / 2.0;
    }else{
        return val;
    }
}

float4 GetColor(float3 pos, int seed){
    float continentCutoff = 50.0;
    float continentScale = 0.75;
    float hillFreq = 5.0;
    float hillAmp = 0.5;

    float mountainCutoff = 100.0;
    float plateFreq = 1.5;

    float humidityCutoff = 10.0;
    float generalHumidity = 0.5;
    float desertScale = 3.0;

    float tempCutoff = 8.0; //must be even
    float iceFreq = 5.0;
    float tempVariation = 0.05;

    //Calculating the height info for the planet. Represented as red.
    float height;

    float heightVal = GetHeight(pos, _Frequency, _Amplitude, _Octaves, seed, true);
    heightVal = saturate(pow(abs(heightVal * continentScale * 2.0), continentCutoff)) / 2.0; //Create defined continents
    heightVal *= GetHeight(pos, 5.0, hillAmp, _Octaves, seed + 3, true) + 1;
    height = heightVal;

    float plateVal = GetHeight(pos, plateFreq, _Amplitude, _Octaves, seed + 1, true);
    float mountainVal = (pow(abs(sin(3.14159 * plateVal)), mountainCutoff)); //Create Mountain Ranges
    mountainVal *= heightVal;
    height += mountainVal;
    height /= 2.0;

    //Calculating the humidity info for the planet. Represented as green.
    float humidityVal = pow(abs(plateVal), desertScale);
    float humidity = generalHumidity * sqrt(1 - pow(abs(2.0 * saturate(humidityVal) - 1.0), humidityCutoff));
    humidity = (-humidityVal + 0.5) * (humidity - generalHumidity) + generalHumidity / 2.0;

    //Calculating the temperature info for the planet. Represented as blue.
    float iceVal = GetHeight(pos, _Frequency * iceFreq, _Amplitude, _Octaves, seed + 2, true);
    float temperature = 1 - pow(pos.y + iceVal * tempVariation, tempCutoff);

    float4 color = float4(height, humidity, temperature, 1.0);
    return color;
}


