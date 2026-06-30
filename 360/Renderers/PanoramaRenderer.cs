using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using static PanoramaViewer.DXInterop;

namespace PanoramaViewer;

internal interface ICameraViewportRenderer
{
    float Yaw { get; set; }

    float Pitch { get; set; }

    float FieldOfView { get; set; }
}

internal abstract class PanoramaRenderer : DXRenderer, ICameraViewportRenderer
{
    private static readonly string VertexShaderSource = """
cbuffer SceneConstants : register(b0)
{
    float4x4 Projection;
};

struct VSInput
{
    float3 Position : POSITION;
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
    output.Position = mul(float4(input.Position, 1.0f), Projection);
    output.TexCoord = input.TexCoord;
    return output;
}
""";

    private static readonly string PixelShaderSource = """
Texture2D PanoramaTexture : register(t0);
SamplerState PanoramaSampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    return PanoramaTexture.Sample(PanoramaSampler, input.TexCoord);
}
""";

    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vertexBuffer;
    private ID3D11Buffer? _indexBuffer;
    private ID3D11Buffer? _constantBuffer;
    private ID3D11Texture2D? _panoramaTexture;
    private ID3D11ShaderResourceView? _panoramaTextureView;
    private ID3D11SamplerState? _samplerState;
    private ID3D11RasterizerState? _rasterizerState;
    private IntPtr _vertexShaderPtr;
    private IntPtr _pixelShaderPtr;
    private IntPtr _inputLayoutPtr;
    private IntPtr _vertexBufferPtr;
    private IntPtr _indexBufferPtr;
    private IntPtr _constantBufferPtr;
    private IntPtr _panoramaTextureViewPtr;
    private IntPtr _samplerStatePtr;
    private IntPtr _rasterizerStatePtr;
    private uint _indexCount;
    private float _yaw;
    private float _pitch;
    private float _fieldOfView = MathF.PI / 2.0f;

    protected override float[] ClearColor => [0.02f, 0.02f, 0.02f, 1.0f];

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

    protected ID3D11Texture2D? PanoramaTexture => _panoramaTexture;

    public abstract void SetSource(Uri source);

    protected abstract Task InitializePanoramaTextureAsync();

    protected virtual bool HasPanoramaSourceResources() => true;

    protected virtual void PreparePanoramaTexture()
    {
    }

    protected virtual void ReleasePanoramaSourceResources()
    {
    }

    protected override async Task InitializeRendererResourcesAsync()
    {
        CreateShaderResources();
        CreateRasterizerState();
        CreateGeometryResources();
        CreateConstantBuffer();
        await InitializePanoramaTextureAsync();
    }

    protected unsafe void CreatePanoramaTextureResources(uint width, uint height)
    {
        D3D11_TEXTURE2D_DESC textureDesc = new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC
            {
                Count = 1,
                Quality = 0,
            },
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        _panoramaTexture = CreateTexture2DNative(D3DDevice!, &textureDesc, null, "Failed to create the panorama texture.");
        _panoramaTextureView = CreateShaderResourceViewNative(D3DDevice!, _panoramaTexture);
        CaptureComInterfacePointer(ref _panoramaTextureViewPtr, _panoramaTextureView, typeof(ID3D11ShaderResourceView).GUID);

        D3D11_SAMPLER_DESC samplerDesc = new()
        {
            Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
            AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_WRAP,
            AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            MipLODBias = 0.0f,
            MaxAnisotropy = 1,
            ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
            MinLOD = 0.0f,
            MaxLOD = float.MaxValue,
        };

        _samplerState = CreateSamplerStateNative(D3DDevice!, &samplerDesc);
        CaptureComInterfacePointer(ref _samplerStatePtr, _samplerState, typeof(ID3D11SamplerState).GUID);
    }

    protected unsafe void UpdatePanoramaTexture(void* pixelData, uint rowPitch)
    {
        D3DContext!.UpdateSubresource(_panoramaTexture!, 0, null, pixelData, rowPitch, 0);
    }

    protected void ReleasePanoramaTextureResources()
    {
        ReleaseComPointer(ref _samplerStatePtr);
        ReleaseComPointer(ref _panoramaTextureViewPtr);
        ReleaseComObject(ref _samplerState);
        ReleaseComObject(ref _panoramaTextureView);
        ReleaseComObject(ref _panoramaTexture);
    }

    protected override bool HasRendererResources()
    {
        return _vertexShader is not null &&
            _pixelShader is not null &&
            _inputLayout is not null &&
            _vertexBuffer is not null &&
            _indexBuffer is not null &&
            _constantBuffer is not null &&
            _panoramaTextureView is not null &&
            _samplerState is not null &&
            _rasterizerState is not null &&
            D3DContextPtr != IntPtr.Zero &&
            _vertexShaderPtr != IntPtr.Zero &&
            _pixelShaderPtr != IntPtr.Zero &&
            _inputLayoutPtr != IntPtr.Zero &&
            _vertexBufferPtr != IntPtr.Zero &&
            _indexBufferPtr != IntPtr.Zero &&
            _constantBufferPtr != IntPtr.Zero &&
            _panoramaTextureViewPtr != IntPtr.Zero &&
            _samplerStatePtr != IntPtr.Zero &&
            _rasterizerStatePtr != IntPtr.Zero &&
            HasPanoramaSourceResources();
    }

    protected override unsafe void RenderFrame()
    {
        UpdateSceneConstants();
        PreparePanoramaTexture();

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
        PSSetShaderResourcesNative(contextPtr, _panoramaTextureViewPtr);
        PSSetSamplersNative(contextPtr, _samplerStatePtr);
        DrawIndexedNative(contextPtr, _indexCount, 0, 0);
    }

    protected override void ReleaseRendererResources()
    {
        ReleasePanoramaSourceResources();
        ReleasePanoramaTextureResources();
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

    private unsafe void CreateShaderResources()
    {
        if (_vertexShader is not null)
        {
            return;
        }

        ID3DBlob vertexShaderBlob = CompileShader(VertexShaderSource, "main", "vs_4_0");
        ID3DBlob pixelShaderBlob = CompileShader(PixelShaderSource, "main", "ps_4_0");

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
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT,
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
                        AlignedByteOffset = 12,
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

        (VertexPositionTexture[] vertices, ushort[] indices) = CreateSphereMesh();
        _indexCount = (uint)indices.Length;

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

        _vertexBuffer = CreateBufferNative(D3DDevice!, &vertexBufferDesc, null, "Failed to create the vertex buffer.");
        _indexBuffer = CreateBufferNative(D3DDevice!, &indexBufferDesc, null, "Failed to create the index buffer.");
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

    private unsafe void CreateConstantBuffer()
    {
        if (_constantBuffer is not null)
        {
            return;
        }

        D3D11_BUFFER_DESC constantBufferDesc = new()
        {
            ByteWidth = (uint)Marshal.SizeOf<SceneConstants>(),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0,
        };

        _constantBuffer = CreateBufferNative(D3DDevice!, &constantBufferDesc, null, "Failed to create the constant buffer.");
        CaptureComInterfacePointer(ref _constantBufferPtr, _constantBuffer, typeof(ID3D11Buffer).GUID);
    }

    private unsafe void UpdateSceneConstants()
    {
        D3D11_VIEWPORT viewport = Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        float aspectRatio = Math.Max(1.0f, viewport.Width) / Math.Max(1.0f, viewport.Height);
        Matrix4x4 world = Matrix4x4.CreateRotationY(_yaw) * Matrix4x4.CreateRotationX(_pitch);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(_fieldOfView, aspectRatio, 0.1f, 10.0f);

        SceneConstants constants = new()
        {
            WorldViewProjection = Matrix4x4.Transpose(world * projection),
        };

        UpdateSubresourceNative(D3DContextPtr, _constantBufferPtr, 0, null, &constants, 0, 0);
    }

    private static (VertexPositionTexture[] Vertices, ushort[] Indices) CreateSphereMesh()
    {
        const int size = 30;

        List<VertexPositionTexture> vertices = new(((size * 2) + 1) * (size + 1));
        List<ushort> indices = new((size * 2) * size * 6);

        for (int i = 0; i <= size; i++)
        {
            float phi = MathF.PI * i / size;

            for (int j = 0; j <= (size * 2); j++)
            {
                float theta = 2.0f * MathF.PI * j / (size * 2);
                float x = MathF.Sin(phi) * MathF.Cos(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Sin(theta);
                float u = j / (float)(size * 2);
                float v = phi / MathF.PI;

                vertices.Add(new VertexPositionTexture(new Vector3(x, y, z), new Vector2(u, v)));
            }
        }

        for (ushort x = 0; x < size; x++)
        {
            for (ushort y = 0; y < (size * 2); y++)
            {
                ushort v0 = (ushort)(x * ((size * 2) + 1) + y);
                ushort v1 = (ushort)((x + 1) * ((size * 2) + 1) + y);
                ushort v2 = (ushort)(x * ((size * 2) + 1) + y + 1);
                ushort v3 = (ushort)((x + 1) * ((size * 2) + 1) + y + 1);

                indices.Add(v0);
                indices.Add(v1);
                indices.Add(v2);
                indices.Add(v2);
                indices.Add(v1);
                indices.Add(v3);
            }
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VertexPositionTexture
    {
        public VertexPositionTexture(Vector3 position, Vector2 textureCoordinate)
        {
            Position = position;
            TextureCoordinate = textureCoordinate;
        }

        public Vector3 Position;

        public Vector2 TextureCoordinate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SceneConstants
    {
        public Matrix4x4 WorldViewProjection;
    }
}
