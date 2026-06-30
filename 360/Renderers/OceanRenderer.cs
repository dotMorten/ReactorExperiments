using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using static PanoramaViewer.DXInterop;

namespace PanoramaViewer;

// Based on https://www.shadertoy.com/view/MdXyzX

internal sealed class OceanRenderer : DXRenderer, ICameraViewportRenderer
{
    private const string VertexShaderSource = """
cbuffer OceanConstants : register(b0)
{
    float4 ResolutionTime;
    float4 CameraPosition;
    float4 CameraForward;
    float4 CameraRight;
    float4 CameraUp;
    float4 OceanParameters;
};

struct VSInput
{
    float2 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position, 0.0f, 1.0f);
    output.TexCoord = input.TexCoord;
    return output;
}
""";

    private const string PixelShaderSource = """
cbuffer OceanConstants : register(b0)
{
    float4 ResolutionTime;
    float4 CameraPosition;
    float4 CameraForward;
    float4 CameraRight;
    float4 CameraUp;
    float4 OceanParameters;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

static const int ITERATIONS_RAYMARCH = 13;
static const int ITERATIONS_NORMAL = 48;

float2 WaveDx(float2 position, float2 direction, float speed, float frequency, float timeShift)
{
    float x = dot(direction, position) * frequency + timeShift * speed;
    float wave = exp(sin(x) - 1.0f);
    float dx = wave * cos(x);
    return float2(wave, -dx);
}

float GetWaves(float2 position, int iterations)
{
    float iter = 0.0f;
    float phase = 6.0f;
    float speed = 2.0f;
    float weight = 1.0f;
    float waves = 0.0f;
    float weightSum = 0.0f;

    [loop]
    for (int i = 0; i < iterations; i++)
    {
        float2 direction = float2(sin(iter), cos(iter));
        float2 wave = WaveDx(position, direction, speed, phase, ResolutionTime.z * OceanParameters.y);
        position += normalize(direction) * wave.y * weight * OceanParameters.w;
        waves += wave.x * weight;
        iter += 12.0f;
        weightSum += weight;
        weight = lerp(weight, 0.0f, 0.2f);
        phase *= 1.18f;
        speed *= 1.07f;
    }

    return waves / max(weightSum, 0.0001f);
}

float WaveHeight(float2 position, float depth)
{
    return GetWaves(position * OceanParameters.x, ITERATIONS_RAYMARCH) * depth * OceanParameters.z - depth;
}

float RaymarchWater(float3 camera, float3 start, float3 end, float depth)
{
    float3 position = start;
    float3 direction = normalize(end - start);

    [loop]
    for (int i = 0; i < 318; i++)
    {
        float height = WaveHeight(position.xz, depth);
        if (height + 0.01f > position.y)
        {
            return distance(position, camera);
        }

        position += direction * (position.y - height);
    }

    return -1.0f;
}

float3 OceanNormal(float2 position, float depth)
{
    float e = 0.001f;
    float h = GetWaves(position * OceanParameters.x, ITERATIONS_NORMAL) * depth * OceanParameters.z;
    float hx = GetWaves((position - float2(e, 0.0f)) * OceanParameters.x, ITERATIONS_NORMAL) * depth * OceanParameters.z;
    float hz = GetWaves((position + float2(0.0f, e)) * OceanParameters.x, ITERATIONS_NORMAL) * depth * OceanParameters.z;

    float3 center = float3(position.x, h, position.y);
    float3 tangentX = normalize(center - float3(position.x - e, hx, position.y));
    float3 tangentZ = normalize(center - float3(position.x, hz, position.y + e));
    return normalize(cross(tangentX, tangentZ));
}

float3 SunDirection()
{
    return normalize(float3(-0.0773502691896258f, 0.5f + sin(ResolutionTime.z * 0.2f + 2.6f) * 0.45f, 0.5773502691896258f));
}

float3 ExtraCheapAtmosphere(float3 rayDirection, float3 sunDirection)
{
    float skyFactor = 1.0f / (rayDirection.y + 0.1f);
    float sunFactor = 1.0f / (sunDirection.y * 11.0f + 1.0f);
    float raySunDotSquared = pow(abs(dot(sunDirection, rayDirection)), 2.0f);
    float3 sunColor = lerp(float3(1.0f, 1.0f, 1.0f), max(float3(0.0f, 0.0f, 0.0f), float3(1.0f, 1.0f, 1.0f) - float3(5.5f, 13.0f, 22.4f) / 22.4f), sunFactor);
    float3 blueSky = float3(5.5f, 13.0f, 22.4f) / 22.4f * sunColor;
    float3 blueSky2 = max(float3(0.0f, 0.0f, 0.0f), blueSky - float3(5.5f, 13.0f, 22.4f) * 0.002f * (skyFactor - 6.0f * sunDirection.y * sunDirection.y));
    blueSky2 *= skyFactor * (0.24f + raySunDotSquared * 0.24f);
    return blueSky2 * (1.0f + pow(1.0f - rayDirection.y, 3.0f));
}

float3 GetAtmosphere(float3 rayDirection)
{
    return ExtraCheapAtmosphere(rayDirection, SunDirection()) * 0.5f;
}

float GetSun(float3 rayDirection)
{
    return pow(max(0.0f, dot(rayDirection, SunDirection())), 720.0f) * 210.0f;
}

float3 Tonemap(float3 color)
{
    float3x3 m1 = float3x3(
        0.59719f, 0.07600f, 0.02840f,
        0.35458f, 0.90834f, 0.13383f,
        0.04823f, 0.01566f, 0.83777f
    );
    float3x3 m2 = float3x3(
        1.60475f, -0.10208f, -0.00327f,
        -0.53108f, 1.10813f, -0.07276f,
        -0.07367f, -0.00605f, 1.07602f
    );

    float3 v = mul(color, m1);
    float3 a = v * (v + 0.0245786f) - 0.000090537f;
    float3 b = v * (0.983729f * v + 0.4329510f) + 0.238081f;
    return pow(saturate(mul(a / b, m2)), float3(1.0f / 2.2f, 1.0f / 2.2f, 1.0f / 2.2f));
}

float IntersectPlane(float3 origin, float3 direction, float3 planePoint, float3 normal)
{
    return clamp(dot(planePoint - origin, normal) / dot(direction, normal), -1.0f, 9991999.0f);
}

float3 GetRay(float2 texCoord)
{
    float aspect = ResolutionTime.x / max(ResolutionTime.y, 1.0f);
    float2 uv = float2(texCoord.x, 1.0f - texCoord.y) * 2.0f - 1.0f;
    uv *= float2(aspect, 1.0f);
    float3 projection = normalize(float3(uv.x, uv.y, 1.0f) + float3(uv.x, uv.y, -1.0f) * pow(length(uv), 2.0f) * 0.05f);
    projection.x *= ResolutionTime.w;
    projection.y *= ResolutionTime.w;
    return normalize(CameraRight.xyz * projection.x + CameraUp.xyz * projection.y + CameraForward.xyz * projection.z);
}

float4 main(PSInput input) : SV_TARGET
{
    const float waterDepth = 2.1f;
    const float3 waterFloor = float3(0.0f, -waterDepth, 0.0f);
    const float3 waterCeiling = float3(0.0f, 0.0f, 0.0f);
    float3 camera = CameraPosition.xyz;
    float3 ray = GetRay(input.TexCoord);
    float3 sky = GetAtmosphere(ray) + GetSun(ray);

    if (ray.y >= -0.01f)
    {
        return float4(Tonemap(sky * 2.0f), 1.0f);
    }

    float highHit = IntersectPlane(camera, ray, waterCeiling, float3(0.0f, 1.0f, 0.0f));
    float lowHit = IntersectPlane(camera, ray, waterFloor, float3(0.0f, 1.0f, 0.0f));
    float3 highPosition = camera + ray * highHit;
    float3 lowPosition = camera + ray * lowHit;
    float distanceToWater = RaymarchWater(camera, highPosition, lowPosition, waterDepth);
    if (distanceToWater < 0.0f)
    {
        return float4(Tonemap(sky * 2.0f), 1.0f);
    }

    float3 worldPosition = camera + ray * distanceToWater;
    float3 normal = OceanNormal(worldPosition.xz, waterDepth);
    normal = lerp(float3(0.0f, 1.0f, 0.0f), normal, 1.0f / (distanceToWater * distanceToWater * 0.01f + 1.0f));
    float3 reflection = normalize(reflect(ray, normal));
    reflection.y = abs(reflection.y);
    float fresnel = 0.04f + 0.96f * pow(1.0f - saturate(dot(-normal, ray)), 5.0f);
    float3 color = fresnel * (GetAtmosphere(reflection) + GetSun(reflection));

    return float4(Tonemap(color * 2.0f), 1.0f);
}
""";

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vertexBuffer;
    private ID3D11Buffer? _indexBuffer;
    private ID3D11Buffer? _constantBuffer;
    private ID3D11RasterizerState? _rasterizerState;
    private IntPtr _vertexShaderPtr;
    private IntPtr _pixelShaderPtr;
    private IntPtr _inputLayoutPtr;
    private IntPtr _vertexBufferPtr;
    private IntPtr _indexBufferPtr;
    private IntPtr _constantBufferPtr;
    private IntPtr _rasterizerStatePtr;
    private Timer? _animationTimer;
    private float _yaw;
    private float _pitch = 0.25f;
    private float _fieldOfView = MathF.PI / 3.0f;
    private float _waveScale = 0.1f;
    private float _waveSpeed = 1.0f;
    private float _waveHeight = 1.0f;
    private float _waveChoppiness = 0.048f;

    public float Yaw
    {
        get => _yaw;
        set
        {
            if (_yaw == value)
            {
                return;
            }

            _yaw = value;
            RequestRender();
        }
    }

    public float Pitch
    {
        get => _pitch;
        set
        {
            if (_pitch == value)
            {
                return;
            }

            _pitch = value;
            RequestRender();
        }
    }

    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            if (_fieldOfView == value)
            {
                return;
            }

            _fieldOfView = value;
            RequestRender();
        }
    }

    public float WaveScale
    {
        get => _waveScale;
        set => SetOceanParameter(ref _waveScale, Math.Clamp(value, 0.02f, 0.3f));
    }

    public float WaveSpeed
    {
        get => _waveSpeed;
        set => SetOceanParameter(ref _waveSpeed, Math.Clamp(value, 0.0f, 3.0f));
    }

    public float WaveHeight
    {
        get => _waveHeight;
        set => SetOceanParameter(ref _waveHeight, Math.Clamp(value, 0.2f, 2.5f));
    }

    public float WaveChoppiness
    {
        get => _waveChoppiness;
        set => SetOceanParameter(ref _waveChoppiness, Math.Clamp(value, 0.0f, 0.15f));
    }

    protected override float[] ClearColor => [0.03f, 0.08f, 0.12f, 1.0f];

    protected override Task InitializeRendererResourcesAsync()
    {
        CreateShaderResources();
        CreateGeometryResources();
        CreateConstantBuffer();
        CreateRasterizerState();
        EnsureAnimationTimer();
        return Task.CompletedTask;
    }

    protected override bool HasRendererResources()
    {
        return _vertexShader is not null &&
            _pixelShader is not null &&
            _inputLayout is not null &&
            _vertexBuffer is not null &&
            _indexBuffer is not null &&
            _constantBuffer is not null &&
            _rasterizerState is not null &&
            D3DContextPtr != IntPtr.Zero &&
            _vertexShaderPtr != IntPtr.Zero &&
            _pixelShaderPtr != IntPtr.Zero &&
            _inputLayoutPtr != IntPtr.Zero &&
            _vertexBufferPtr != IntPtr.Zero &&
            _indexBufferPtr != IntPtr.Zero &&
            _constantBufferPtr != IntPtr.Zero &&
            _rasterizerStatePtr != IntPtr.Zero;
    }

    protected override unsafe void RenderFrame()
    {
        UpdateConstants();

        IntPtr contextPtr = D3DContextPtr;
        RSSetStateNative(contextPtr, _rasterizerStatePtr);
        IASetInputLayoutNative(contextPtr, _inputLayoutPtr);
        uint stride = (uint)Marshal.SizeOf<VertexPositionTexture>();
        uint offset = 0;
        IASetVertexBuffersNative(contextPtr, _vertexBufferPtr, stride, offset);
        IASetIndexBufferNative(contextPtr, _indexBufferPtr, DXGI_FORMAT.DXGI_FORMAT_R16_UINT, 0);
        IASetPrimitiveTopologyNative(contextPtr, D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        VSSetShaderNative(contextPtr, _vertexShaderPtr);
        VSSetConstantBuffersNative(contextPtr, _constantBufferPtr);
        PSSetShaderNative(contextPtr, _pixelShaderPtr);
        PSSetConstantBuffersNative(contextPtr, _constantBufferPtr);
        DrawIndexedNative(contextPtr, 6, 0, 0);
    }

    protected override void ReleaseRendererResources()
    {
        StopAnimationTimer();
        ReleaseComPointer(ref _rasterizerStatePtr);
        ReleaseComPointer(ref _constantBufferPtr);
        ReleaseComPointer(ref _indexBufferPtr);
        ReleaseComPointer(ref _vertexBufferPtr);
        ReleaseComPointer(ref _inputLayoutPtr);
        ReleaseComPointer(ref _pixelShaderPtr);
        ReleaseComPointer(ref _vertexShaderPtr);
        ReleaseComObject(ref _rasterizerState);
        ReleaseComObject(ref _constantBuffer);
        ReleaseComObject(ref _indexBuffer);
        ReleaseComObject(ref _vertexBuffer);
        ReleaseComObject(ref _inputLayout);
        ReleaseComObject(ref _pixelShader);
        ReleaseComObject(ref _vertexShader);
    }

    private void EnsureAnimationTimer()
    {
        _animationTimer ??= new Timer(
            static state => ((OceanRenderer)state!).RequestRender(),
            this,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(16));
    }

    private void StopAnimationTimer()
    {
        _animationTimer?.Dispose();
        _animationTimer = null;
    }

    private unsafe void CreateShaderResources()
    {
        if (_vertexShader is not null)
        {
            return;
        }

        ID3DBlob vertexShaderBlob = CompileShader(VertexShaderSource, "main", "vs_5_0");
        ID3DBlob pixelShaderBlob = CompileShader(PixelShaderSource, "main", "ps_5_0");

        try
        {
            _vertexShader = CreateVertexShaderNative(D3DDevice!, vertexShaderBlob.GetBufferPointer(), vertexShaderBlob.GetBufferSize());
            _pixelShader = CreatePixelShaderNative(D3DDevice!, pixelShaderBlob.GetBufferPointer(), pixelShaderBlob.GetBufferSize());
            CaptureComInterfacePointer(ref _vertexShaderPtr, _vertexShader, typeof(ID3D11VertexShader).GUID);
            CaptureComInterfacePointer(ref _pixelShaderPtr, _pixelShader, typeof(ID3D11PixelShader).GUID);

            byte[] positionSemantic = Encoding.ASCII.GetBytes("POSITION\0");
            byte[] texCoordSemantic = Encoding.ASCII.GetBytes("TEXCOORD\0");

            fixed (byte* positionSemanticPtr = positionSemantic)
            fixed (byte* texCoordSemanticPtr = texCoordSemantic)
            {
                D3D11_INPUT_ELEMENT_DESC[] inputElements =
                [
                    new D3D11_INPUT_ELEMENT_DESC
                    {
                        SemanticName = (Windows.Win32.Foundation.PCSTR)positionSemanticPtr,
                        SemanticIndex = 0,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        InputSlot = 0,
                        AlignedByteOffset = 0,
                        InputSlotClass = D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA,
                        InstanceDataStepRate = 0,
                    },
                    new D3D11_INPUT_ELEMENT_DESC
                    {
                        SemanticName = (Windows.Win32.Foundation.PCSTR)texCoordSemanticPtr,
                        SemanticIndex = 0,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        InputSlot = 0,
                        AlignedByteOffset = 8,
                        InputSlotClass = D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA,
                        InstanceDataStepRate = 0,
                    },
                ];

                fixed (D3D11_INPUT_ELEMENT_DESC* inputElementsPtr = inputElements)
                {
                    _inputLayout = CreateInputLayoutNative(D3DDevice!, inputElementsPtr, (uint)inputElements.Length, vertexShaderBlob.GetBufferPointer(), vertexShaderBlob.GetBufferSize());
                    CaptureComInterfacePointer(ref _inputLayoutPtr, _inputLayout, typeof(ID3D11InputLayout).GUID);
                }
            }
        }
        finally
        {
            ReleaseComObject(ref pixelShaderBlob!);
            ReleaseComObject(ref vertexShaderBlob!);
        }
    }

    private unsafe void CreateGeometryResources()
    {
        if (_vertexBuffer is not null && _indexBuffer is not null)
        {
            return;
        }

        VertexPositionTexture[] vertices =
        [
            new(new Vector2(-1.0f, 1.0f), new Vector2(0.0f, 0.0f)),
            new(new Vector2(1.0f, 1.0f), new Vector2(1.0f, 0.0f)),
            new(new Vector2(1.0f, -1.0f), new Vector2(1.0f, 1.0f)),
            new(new Vector2(-1.0f, -1.0f), new Vector2(0.0f, 1.0f)),
        ];

        ushort[] indices = [0, 1, 2, 0, 2, 3];

        D3D11_BUFFER_DESC vertexBufferDesc = new()
        {
            ByteWidth = (uint)(vertices.Length * Marshal.SizeOf<VertexPositionTexture>()),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0,
        };

        D3D11_BUFFER_DESC indexBufferDesc = new()
        {
            ByteWidth = (uint)(indices.Length * sizeof(ushort)),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_INDEX_BUFFER,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0,
        };

        _vertexBuffer = CreateBufferNative(D3DDevice!, &vertexBufferDesc, null, "Failed to create the ocean vertex buffer.");
        _indexBuffer = CreateBufferNative(D3DDevice!, &indexBufferDesc, null, "Failed to create the ocean index buffer.");
        CaptureComInterfacePointer(ref _vertexBufferPtr, _vertexBuffer, typeof(ID3D11Buffer).GUID);
        CaptureComInterfacePointer(ref _indexBufferPtr, _indexBuffer, typeof(ID3D11Buffer).GUID);

        fixed (VertexPositionTexture* vertexData = vertices)
        {
            D3DContext!.UpdateSubresource(_vertexBuffer!, 0, null, vertexData, 0, 0);
        }

        fixed (ushort* indexData = indices)
        {
            D3DContext!.UpdateSubresource(_indexBuffer!, 0, null, indexData, 0, 0);
        }
    }

    private unsafe void CreateConstantBuffer()
    {
        if (_constantBuffer is not null)
        {
            return;
        }

        D3D11_BUFFER_DESC constantBufferDesc = new()
        {
            ByteWidth = (uint)Marshal.SizeOf<OceanConstants>(),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0,
        };

        _constantBuffer = CreateBufferNative(D3DDevice!, &constantBufferDesc, null, "Failed to create the ocean constant buffer.");
        CaptureComInterfacePointer(ref _constantBufferPtr, _constantBuffer, typeof(ID3D11Buffer).GUID);
    }

    private unsafe void CreateRasterizerState()
    {
        if (_rasterizerState is not null)
        {
            return;
        }

        D3D11_RASTERIZER_DESC rasterizerDesc = new()
        {
            FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID,
            CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE,
            FrontCounterClockwise = false,
            DepthBias = 0,
            DepthBiasClamp = 0.0f,
            SlopeScaledDepthBias = 0.0f,
            DepthClipEnable = true,
            ScissorEnable = false,
            MultisampleEnable = false,
            AntialiasedLineEnable = false,
        };

        _rasterizerState = CreateRasterizerStateNative(D3DDevice!, &rasterizerDesc);
        CaptureComInterfacePointer(ref _rasterizerStatePtr, _rasterizerState, typeof(ID3D11RasterizerState).GUID);
    }

    private unsafe void UpdateConstants()
    {
        D3D11_VIEWPORT viewport = Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        Vector3 forward = GetForwardVector();
        Vector3 right = Vector3.Cross(Vector3.UnitY, forward);
        if (right.LengthSquared() < 1e-5f)
        {
            right = Vector3.UnitX;
        }
        else
        {
            right = Vector3.Normalize(right);
        }

        Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

        OceanConstants constants = new()
        {
            ResolutionTime = new Vector4(viewport.Width, viewport.Height, (float)_stopwatch.Elapsed.TotalSeconds, MathF.Tan(_fieldOfView * 0.5f)),
            CameraPosition = new Vector4(0.0f, 2.0f, 0.0f, 0.0f),
            CameraForward = new Vector4(forward, 0.0f),
            CameraRight = new Vector4(right, 0.0f),
            CameraUp = new Vector4(up, 0.0f),
            OceanParameters = new Vector4(_waveScale, _waveSpeed, _waveHeight, _waveChoppiness),
        };

        UpdateSubresourceNative(D3DContextPtr, _constantBufferPtr, 0, null, &constants, 0, 0);
    }

    private void SetOceanParameter(ref float field, float value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        RequestRender();
    }

    private Vector3 GetForwardVector()
    {
        float cosPitch = MathF.Cos(-_pitch);
        Vector3 forward = new(
            MathF.Sin(_yaw) * cosPitch,
            MathF.Sin(-_pitch),
            MathF.Cos(_yaw) * cosPitch);

        return Vector3.Normalize(forward);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VertexPositionTexture
    {
        public VertexPositionTexture(Vector2 position, Vector2 textureCoordinate)
        {
            Position = position;
            TextureCoordinate = textureCoordinate;
        }

        public Vector2 Position;

        public Vector2 TextureCoordinate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OceanConstants
    {
        public Vector4 ResolutionTime;
        public Vector4 CameraPosition;
        public Vector4 CameraForward;
        public Vector4 CameraRight;
        public Vector4 CameraUp;
        public Vector4 OceanParameters;
    }
}
