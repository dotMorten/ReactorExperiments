using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace PanoramaViewer;

internal static class DXInterop
{
    internal static IntPtr GetComInterfacePointer(object comObject, Guid interfaceId)
    {
        IntPtr unknown = Marshal.GetIUnknownForObject(comObject);

        try
        {
            int hr = Marshal.QueryInterface(unknown, in interfaceId, out IntPtr interfacePtr);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return interfacePtr;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    internal static T GetComObjectFromInterfacePointer<T>(IntPtr interfacePtr) where T : class
    {
        if (interfacePtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to create the {typeof(T).Name} COM object.");
        }

        try
        {
            return (T)Marshal.GetObjectForIUnknown(interfacePtr);
        }
        finally
        {
            Marshal.Release(interfacePtr);
        }
    }

    internal static unsafe void OMSetRenderTargetsNative(ID3D11DeviceContext deviceContext, ID3D11RenderTargetView[] renderTargets)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr* renderTargetPtrs = stackalloc IntPtr[renderTargets.Length];

        try
        {
            for (int i = 0; i < renderTargets.Length; i++)
            {
                renderTargetPtrs[i] = GetComInterfacePointer(renderTargets[i], typeof(ID3D11RenderTargetView).GUID);
            }

            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var omSetRenderTargets = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, IntPtr, void>)vtable[33];
            omSetRenderTargets(deviceContextPtr, (uint)renderTargets.Length, renderTargetPtrs, IntPtr.Zero);
        }
        finally
        {
            for (int i = 0; i < renderTargets.Length; i++)
            {
                if (renderTargetPtrs[i] != IntPtr.Zero)
                {
                    Marshal.Release(renderTargetPtrs[i]);
                }
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void OMSetRenderTargetsNative(IntPtr deviceContextPtr, IntPtr renderTargetViewPtr)
    {
        IntPtr* renderTargetPtrs = stackalloc IntPtr[1];
        renderTargetPtrs[0] = renderTargetViewPtr;

        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var omSetRenderTargets = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, IntPtr, void>)vtable[33];
        omSetRenderTargets(deviceContextPtr, 1, renderTargetPtrs, IntPtr.Zero);
    }

    internal static unsafe void IASetVertexBuffersNative(ID3D11DeviceContext deviceContext, ID3D11Buffer[] vertexBuffers, uint stride, uint offset)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr* bufferPtrs = stackalloc IntPtr[vertexBuffers.Length];

        try
        {
            for (int i = 0; i < vertexBuffers.Length; i++)
            {
                bufferPtrs[i] = GetComInterfacePointer(vertexBuffers[i], typeof(ID3D11Buffer).GUID);
            }

            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var iaSetVertexBuffers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, uint*, uint*, void>)vtable[18];
            iaSetVertexBuffers(deviceContextPtr, 0, (uint)vertexBuffers.Length, bufferPtrs, &stride, &offset);
        }
        finally
        {
            for (int i = 0; i < vertexBuffers.Length; i++)
            {
                if (bufferPtrs[i] != IntPtr.Zero)
                {
                    Marshal.Release(bufferPtrs[i]);
                }
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void IASetVertexBuffersNative(IntPtr deviceContextPtr, IntPtr vertexBufferPtr, uint stride, uint offset)
    {
        IntPtr* bufferPtrs = stackalloc IntPtr[1];
        bufferPtrs[0] = vertexBufferPtr;

        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var iaSetVertexBuffers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, uint*, uint*, void>)vtable[18];
        iaSetVertexBuffers(deviceContextPtr, 0, 1, bufferPtrs, &stride, &offset);
    }

    internal static unsafe void VSSetShaderNative(ID3D11DeviceContext deviceContext, ID3D11VertexShader shader)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr shaderPtr = IntPtr.Zero;

        try
        {
            shaderPtr = GetComInterfacePointer(shader, typeof(ID3D11VertexShader).GUID);
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var vsSetShader = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>)vtable[11];
            vsSetShader(deviceContextPtr, shaderPtr, null, 0);
        }
        finally
        {
            if (shaderPtr != IntPtr.Zero)
            {
                Marshal.Release(shaderPtr);
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void VSSetShaderNative(IntPtr deviceContextPtr, IntPtr shaderPtr)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var vsSetShader = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>)vtable[11];
        vsSetShader(deviceContextPtr, shaderPtr, null, 0);
    }

    internal static unsafe void VSSetConstantBuffersNative(ID3D11DeviceContext deviceContext, ID3D11Buffer[] constantBuffers)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr* bufferPtrs = stackalloc IntPtr[constantBuffers.Length];

        try
        {
            for (int i = 0; i < constantBuffers.Length; i++)
            {
                bufferPtrs[i] = GetComInterfacePointer(constantBuffers[i], typeof(ID3D11Buffer).GUID);
            }

            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var vsSetConstantBuffers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[7];
            vsSetConstantBuffers(deviceContextPtr, 0, (uint)constantBuffers.Length, bufferPtrs);
        }
        finally
        {
            for (int i = 0; i < constantBuffers.Length; i++)
            {
                if (bufferPtrs[i] != IntPtr.Zero)
                {
                    Marshal.Release(bufferPtrs[i]);
                }
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void VSSetConstantBuffersNative(IntPtr deviceContextPtr, IntPtr constantBufferPtr)
    {
        IntPtr* bufferPtrs = stackalloc IntPtr[1];
        bufferPtrs[0] = constantBufferPtr;

        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var vsSetConstantBuffers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[7];
        vsSetConstantBuffers(deviceContextPtr, 0, 1, bufferPtrs);
    }

    internal static unsafe void PSSetConstantBuffersNative(ID3D11DeviceContext deviceContext, ID3D11Buffer[] constantBuffers)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr* bufferPtrs = stackalloc IntPtr[constantBuffers.Length];

        try
        {
            for (int i = 0; i < constantBuffers.Length; i++)
            {
                bufferPtrs[i] = GetComInterfacePointer(constantBuffers[i], typeof(ID3D11Buffer).GUID);
            }

            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var psSetConstantBuffers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[16];
            psSetConstantBuffers(deviceContextPtr, 0, (uint)constantBuffers.Length, bufferPtrs);
        }
        finally
        {
            for (int i = 0; i < constantBuffers.Length; i++)
            {
                if (bufferPtrs[i] != IntPtr.Zero)
                {
                    Marshal.Release(bufferPtrs[i]);
                }
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void PSSetConstantBuffersNative(IntPtr deviceContextPtr, IntPtr constantBufferPtr)
    {
        IntPtr* bufferPtrs = stackalloc IntPtr[1];
        bufferPtrs[0] = constantBufferPtr;

        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var psSetConstantBuffers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[16];
        psSetConstantBuffers(deviceContextPtr, 0, 1, bufferPtrs);
    }

    internal static unsafe void PSSetShaderNative(ID3D11DeviceContext deviceContext, ID3D11PixelShader shader)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr shaderPtr = IntPtr.Zero;

        try
        {
            shaderPtr = GetComInterfacePointer(shader, typeof(ID3D11PixelShader).GUID);
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var psSetShader = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>)vtable[9];
            psSetShader(deviceContextPtr, shaderPtr, null, 0);
        }
        finally
        {
            if (shaderPtr != IntPtr.Zero)
            {
                Marshal.Release(shaderPtr);
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void PSSetShaderNative(IntPtr deviceContextPtr, IntPtr shaderPtr)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var psSetShader = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>)vtable[9];
        psSetShader(deviceContextPtr, shaderPtr, null, 0);
    }

    internal static unsafe void PSSetShaderResourcesNative(ID3D11DeviceContext deviceContext, ID3D11ShaderResourceView[] shaderResources)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr* resourcePtrs = stackalloc IntPtr[shaderResources.Length];

        try
        {
            for (int i = 0; i < shaderResources.Length; i++)
            {
                resourcePtrs[i] = GetComInterfacePointer(shaderResources[i], typeof(ID3D11ShaderResourceView).GUID);
            }

            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var psSetShaderResources = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[8];
            psSetShaderResources(deviceContextPtr, 0, (uint)shaderResources.Length, resourcePtrs);
        }
        finally
        {
            for (int i = 0; i < shaderResources.Length; i++)
            {
                if (resourcePtrs[i] != IntPtr.Zero)
                {
                    Marshal.Release(resourcePtrs[i]);
                }
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void PSSetShaderResourcesNative(IntPtr deviceContextPtr, IntPtr shaderResourceViewPtr)
    {
        IntPtr* resourcePtrs = stackalloc IntPtr[1];
        resourcePtrs[0] = shaderResourceViewPtr;

        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var psSetShaderResources = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[8];
        psSetShaderResources(deviceContextPtr, 0, 1, resourcePtrs);
    }

    internal static unsafe void PSSetSamplersNative(ID3D11DeviceContext deviceContext, ID3D11SamplerState[] samplers)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr* samplerPtrs = stackalloc IntPtr[samplers.Length];

        try
        {
            for (int i = 0; i < samplers.Length; i++)
            {
                samplerPtrs[i] = GetComInterfacePointer(samplers[i], typeof(ID3D11SamplerState).GUID);
            }

            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var psSetSamplers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[10];
            psSetSamplers(deviceContextPtr, 0, (uint)samplers.Length, samplerPtrs);
        }
        finally
        {
            for (int i = 0; i < samplers.Length; i++)
            {
                if (samplerPtrs[i] != IntPtr.Zero)
                {
                    Marshal.Release(samplerPtrs[i]);
                }
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void PSSetSamplersNative(IntPtr deviceContextPtr, IntPtr samplerPtr)
    {
        IntPtr* samplerPtrs = stackalloc IntPtr[1];
        samplerPtrs[0] = samplerPtr;

        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var psSetSamplers = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>)vtable[10];
        psSetSamplers(deviceContextPtr, 0, 1, samplerPtrs);
    }

    internal static unsafe void ClearRenderTargetViewNative(ID3D11DeviceContext deviceContext, ID3D11RenderTargetView renderTargetView, float[] color)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr renderTargetViewPtr = IntPtr.Zero;

        try
        {
            renderTargetViewPtr = GetComInterfacePointer(renderTargetView, typeof(ID3D11RenderTargetView).GUID);
            fixed (float* colorPtr = color)
            {
                IntPtr* vtable = *(IntPtr**)deviceContextPtr;
                var clearRenderTargetView = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, float*, void>)vtable[50];
                clearRenderTargetView(deviceContextPtr, renderTargetViewPtr, colorPtr);
            }
        }
        finally
        {
            if (renderTargetViewPtr != IntPtr.Zero)
            {
                Marshal.Release(renderTargetViewPtr);
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void ClearRenderTargetViewNative(IntPtr deviceContextPtr, IntPtr renderTargetViewPtr, float[] color)
    {
        fixed (float* colorPtr = color)
        {
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var clearRenderTargetView = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, float*, void>)vtable[50];
            clearRenderTargetView(deviceContextPtr, renderTargetViewPtr, colorPtr);
        }
    }

    internal static unsafe void RSSetViewportsNative(ID3D11DeviceContext deviceContext, D3D11_VIEWPORT* viewports, uint viewportCount)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);

        try
        {
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var rsSetViewports = (delegate* unmanaged[Stdcall]<IntPtr, uint, D3D11_VIEWPORT*, void>)vtable[44];
            rsSetViewports(deviceContextPtr, viewportCount, viewports);
        }
        finally
        {
            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void RSSetViewportsNative(IntPtr deviceContextPtr, D3D11_VIEWPORT* viewports, uint viewportCount)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var rsSetViewports = (delegate* unmanaged[Stdcall]<IntPtr, uint, D3D11_VIEWPORT*, void>)vtable[44];
        rsSetViewports(deviceContextPtr, viewportCount, viewports);
    }

    internal static unsafe void UpdateSubresourceNative(ID3D11DeviceContext deviceContext, ID3D11Resource resource, uint destinationSubresource, D3D11_BOX* destinationBox, void* sourceData, uint sourceRowPitch, uint sourceDepthPitch)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr resourcePtr = IntPtr.Zero;

        try
        {
            resourcePtr = GetComInterfacePointer(resource, typeof(ID3D11Resource).GUID);
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var updateSubresource = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, D3D11_BOX*, void*, uint, uint, void>)vtable[48];
            updateSubresource(deviceContextPtr, resourcePtr, destinationSubresource, destinationBox, sourceData, sourceRowPitch, sourceDepthPitch);
        }
        finally
        {
            if (resourcePtr != IntPtr.Zero)
            {
                Marshal.Release(resourcePtr);
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void UpdateSubresourceNative(IntPtr deviceContextPtr, IntPtr resourcePtr, uint destinationSubresource, D3D11_BOX* destinationBox, void* sourceData, uint sourceRowPitch, uint sourceDepthPitch)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var updateSubresource = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, D3D11_BOX*, void*, uint, uint, void>)vtable[48];
        updateSubresource(deviceContextPtr, resourcePtr, destinationSubresource, destinationBox, sourceData, sourceRowPitch, sourceDepthPitch);
    }

    internal static unsafe void RSSetStateNative(ID3D11DeviceContext deviceContext, ID3D11RasterizerState rasterizerState)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr rasterizerStatePtr = IntPtr.Zero;

        try
        {
            rasterizerStatePtr = GetComInterfacePointer(rasterizerState, typeof(ID3D11RasterizerState).GUID);
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var rsSetState = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>)vtable[43];
            rsSetState(deviceContextPtr, rasterizerStatePtr);
        }
        finally
        {
            if (rasterizerStatePtr != IntPtr.Zero)
            {
                Marshal.Release(rasterizerStatePtr);
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void RSSetStateNative(IntPtr deviceContextPtr, IntPtr rasterizerStatePtr)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var rsSetState = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>)vtable[43];
        rsSetState(deviceContextPtr, rasterizerStatePtr);
    }

    internal static unsafe void IASetInputLayoutNative(ID3D11DeviceContext deviceContext, ID3D11InputLayout inputLayout)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr inputLayoutPtr = IntPtr.Zero;

        try
        {
            inputLayoutPtr = GetComInterfacePointer(inputLayout, typeof(ID3D11InputLayout).GUID);
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var iaSetInputLayout = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>)vtable[17];
            iaSetInputLayout(deviceContextPtr, inputLayoutPtr);
        }
        finally
        {
            if (inputLayoutPtr != IntPtr.Zero)
            {
                Marshal.Release(inputLayoutPtr);
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void IASetInputLayoutNative(IntPtr deviceContextPtr, IntPtr inputLayoutPtr)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var iaSetInputLayout = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>)vtable[17];
        iaSetInputLayout(deviceContextPtr, inputLayoutPtr);
    }

    internal static unsafe void IASetIndexBufferNative(ID3D11DeviceContext deviceContext, ID3D11Buffer indexBuffer, DXGI_FORMAT format, uint offset)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);
        IntPtr indexBufferPtr = IntPtr.Zero;

        try
        {
            indexBufferPtr = GetComInterfacePointer(indexBuffer, typeof(ID3D11Buffer).GUID);
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var iaSetIndexBuffer = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DXGI_FORMAT, uint, void>)vtable[19];
            iaSetIndexBuffer(deviceContextPtr, indexBufferPtr, format, offset);
        }
        finally
        {
            if (indexBufferPtr != IntPtr.Zero)
            {
                Marshal.Release(indexBufferPtr);
            }

            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void IASetIndexBufferNative(IntPtr deviceContextPtr, IntPtr indexBufferPtr, DXGI_FORMAT format, uint offset)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var iaSetIndexBuffer = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DXGI_FORMAT, uint, void>)vtable[19];
        iaSetIndexBuffer(deviceContextPtr, indexBufferPtr, format, offset);
    }

    internal static unsafe void IASetPrimitiveTopologyNative(ID3D11DeviceContext deviceContext, D3D_PRIMITIVE_TOPOLOGY topology)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);

        try
        {
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var iaSetPrimitiveTopology = (delegate* unmanaged[Stdcall]<IntPtr, D3D_PRIMITIVE_TOPOLOGY, void>)vtable[24];
            iaSetPrimitiveTopology(deviceContextPtr, topology);
        }
        finally
        {
            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void IASetPrimitiveTopologyNative(IntPtr deviceContextPtr, D3D_PRIMITIVE_TOPOLOGY topology)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var iaSetPrimitiveTopology = (delegate* unmanaged[Stdcall]<IntPtr, D3D_PRIMITIVE_TOPOLOGY, void>)vtable[24];
        iaSetPrimitiveTopology(deviceContextPtr, topology);
    }

    internal static unsafe void DrawIndexedNative(ID3D11DeviceContext deviceContext, uint indexCount, uint startIndexLocation, int baseVertexLocation)
    {
        IntPtr deviceContextPtr = GetComInterfacePointer(deviceContext, typeof(ID3D11DeviceContext).GUID);

        try
        {
            IntPtr* vtable = *(IntPtr**)deviceContextPtr;
            var drawIndexed = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int, void>)vtable[12];
            drawIndexed(deviceContextPtr, indexCount, startIndexLocation, baseVertexLocation);
        }
        finally
        {
            Marshal.Release(deviceContextPtr);
        }
    }

    internal static unsafe void DrawIndexedNative(IntPtr deviceContextPtr, uint indexCount, uint startIndexLocation, int baseVertexLocation)
    {
        IntPtr* vtable = *(IntPtr**)deviceContextPtr;
        var drawIndexed = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int, void>)vtable[12];
        drawIndexed(deviceContextPtr, indexCount, startIndexLocation, baseVertexLocation);
    }

    internal static unsafe void PresentNative(IDXGISwapChain1 swapChain, uint syncInterval, uint flags)
    {
        IntPtr swapChainPtr = GetComInterfacePointer(swapChain, typeof(IDXGISwapChain).GUID);

        try
        {
            IntPtr* vtable = *(IntPtr**)swapChainPtr;
            var present = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)vtable[8];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(present(swapChainPtr, syncInterval, flags)), "Failed to present the swap chain.");
        }
        finally
        {
            Marshal.Release(swapChainPtr);
        }
    }

    internal static unsafe void PresentNative(IntPtr swapChainPtr, uint syncInterval, uint flags)
    {
        IntPtr* vtable = *(IntPtr**)swapChainPtr;
        var present = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)vtable[8];
        ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(present(swapChainPtr, syncInterval, flags)), "Failed to present the swap chain.");
    }

    internal static unsafe ID3D11Buffer CreateBufferNative(ID3D11Device device, D3D11_BUFFER_DESC* bufferDesc, D3D11_SUBRESOURCE_DATA* initialData, string message)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr bufferPtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createBuffer = (delegate* unmanaged[Stdcall]<IntPtr, D3D11_BUFFER_DESC*, D3D11_SUBRESOURCE_DATA*, IntPtr*, int>)vtable[3];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createBuffer(devicePtr, bufferDesc, initialData, &bufferPtr)), message);
            return GetComObjectFromInterfacePointer<ID3D11Buffer>(bufferPtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11Texture2D CreateTexture2DNative(ID3D11Device device, D3D11_TEXTURE2D_DESC* textureDesc, D3D11_SUBRESOURCE_DATA* initialData, string message)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr texturePtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createTexture2D = (delegate* unmanaged[Stdcall]<IntPtr, D3D11_TEXTURE2D_DESC*, D3D11_SUBRESOURCE_DATA*, IntPtr*, int>)vtable[5];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createTexture2D(devicePtr, textureDesc, initialData, &texturePtr)), message);
            return GetComObjectFromInterfacePointer<ID3D11Texture2D>(texturePtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11RenderTargetView CreateRenderTargetViewNative(ID3D11Device device, ID3D11Texture2D resource)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr resourcePtr = GetComInterfacePointer(resource, typeof(ID3D11Resource).GUID);
        IntPtr renderTargetViewPtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createRenderTargetView = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, D3D11_RENDER_TARGET_VIEW_DESC*, IntPtr*, int>)vtable[9];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createRenderTargetView(devicePtr, resourcePtr, null, &renderTargetViewPtr)), "Failed to create the render target view.");
            return GetComObjectFromInterfacePointer<ID3D11RenderTargetView>(renderTargetViewPtr);
        }
        finally
        {
            Marshal.Release(resourcePtr);
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11DepthStencilView CreateDepthStencilViewNative(ID3D11Device device, ID3D11Texture2D resource)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr resourcePtr = GetComInterfacePointer(resource, typeof(ID3D11Resource).GUID);
        IntPtr depthStencilViewPtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createDepthStencilView = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, D3D11_DEPTH_STENCIL_VIEW_DESC*, IntPtr*, int>)vtable[10];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createDepthStencilView(devicePtr, resourcePtr, null, &depthStencilViewPtr)), "Failed to create the depth stencil view.");
            return GetComObjectFromInterfacePointer<ID3D11DepthStencilView>(depthStencilViewPtr);
        }
        finally
        {
            Marshal.Release(resourcePtr);
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11InputLayout CreateInputLayoutNative(ID3D11Device device, D3D11_INPUT_ELEMENT_DESC* inputElements, uint inputElementCount, void* shaderBytecode, nuint shaderBytecodeLength)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr inputLayoutPtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createInputLayout = (delegate* unmanaged[Stdcall]<IntPtr, D3D11_INPUT_ELEMENT_DESC*, uint, void*, nuint, IntPtr*, int>)vtable[11];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createInputLayout(devicePtr, inputElements, inputElementCount, shaderBytecode, shaderBytecodeLength, &inputLayoutPtr)), "Failed to create the input layout.");
            return GetComObjectFromInterfacePointer<ID3D11InputLayout>(inputLayoutPtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11VertexShader CreateVertexShaderNative(ID3D11Device device, void* shaderBytecode, nuint shaderBytecodeLength)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr vertexShaderPtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createVertexShader = (delegate* unmanaged[Stdcall]<IntPtr, void*, nuint, IntPtr, IntPtr*, int>)vtable[12];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createVertexShader(devicePtr, shaderBytecode, shaderBytecodeLength, IntPtr.Zero, &vertexShaderPtr)), "Failed to create the vertex shader.");
            return GetComObjectFromInterfacePointer<ID3D11VertexShader>(vertexShaderPtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11PixelShader CreatePixelShaderNative(ID3D11Device device, void* shaderBytecode, nuint shaderBytecodeLength)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr pixelShaderPtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createPixelShader = (delegate* unmanaged[Stdcall]<IntPtr, void*, nuint, IntPtr, IntPtr*, int>)vtable[15];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createPixelShader(devicePtr, shaderBytecode, shaderBytecodeLength, IntPtr.Zero, &pixelShaderPtr)), "Failed to create the pixel shader.");
            return GetComObjectFromInterfacePointer<ID3D11PixelShader>(pixelShaderPtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11ShaderResourceView CreateShaderResourceViewNative(ID3D11Device device, ID3D11Texture2D resource)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr resourcePtr = GetComInterfacePointer(resource, typeof(ID3D11Resource).GUID);
        IntPtr shaderResourceViewPtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createShaderResourceView = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, D3D11_SHADER_RESOURCE_VIEW_DESC*, IntPtr*, int>)vtable[7];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createShaderResourceView(devicePtr, resourcePtr, null, &shaderResourceViewPtr)), "Failed to create the shader resource view.");
            return GetComObjectFromInterfacePointer<ID3D11ShaderResourceView>(shaderResourceViewPtr);
        }
        finally
        {
            Marshal.Release(resourcePtr);
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11RasterizerState CreateRasterizerStateNative(ID3D11Device device, D3D11_RASTERIZER_DESC* rasterizerDesc)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr rasterizerStatePtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createRasterizerState = (delegate* unmanaged[Stdcall]<IntPtr, D3D11_RASTERIZER_DESC*, IntPtr*, int>)vtable[22];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createRasterizerState(devicePtr, rasterizerDesc, &rasterizerStatePtr)), "Failed to create the rasterizer state.");
            return GetComObjectFromInterfacePointer<ID3D11RasterizerState>(rasterizerStatePtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3D11SamplerState CreateSamplerStateNative(ID3D11Device device, D3D11_SAMPLER_DESC* samplerDesc)
    {
        IntPtr devicePtr = GetComInterfacePointer(device, typeof(ID3D11Device).GUID);
        IntPtr samplerStatePtr = IntPtr.Zero;

        try
        {
            IntPtr* vtable = *(IntPtr**)devicePtr;
            var createSamplerState = (delegate* unmanaged[Stdcall]<IntPtr, D3D11_SAMPLER_DESC*, IntPtr*, int>)vtable[23];
            ThrowIfFailed(new Windows.Win32.Foundation.HRESULT(createSamplerState(devicePtr, samplerDesc, &samplerStatePtr)), "Failed to create the sampler state.");
            return GetComObjectFromInterfacePointer<ID3D11SamplerState>(samplerStatePtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    internal static unsafe ID3DBlob CompileShader(string source, string entryPoint, string shaderTarget)
    {
        byte[] sourceBytes = Encoding.UTF8.GetBytes(source);
        byte[] entryPointBytes = Encoding.ASCII.GetBytes(entryPoint + "\0");
        byte[] shaderTargetBytes = Encoding.ASCII.GetBytes(shaderTarget + "\0");

        fixed (byte* sourcePtr = sourceBytes)
        fixed (byte* entryPointPtr = entryPointBytes)
        fixed (byte* shaderTargetPtr = shaderTargetBytes)
        {
            Windows.Win32.Foundation.HRESULT result = Windows.Win32.PInvoke.D3DCompile(
                sourcePtr,
                (nuint)sourceBytes.Length,
                default,
                (D3D_SHADER_MACRO*)null,
                null,
                (Windows.Win32.Foundation.PCSTR)entryPointPtr,
                (Windows.Win32.Foundation.PCSTR)shaderTargetPtr,
                0,
                0,
                out ID3DBlob shaderBlob,
                out ID3DBlob errorBlob);

            if (result.Value < 0)
            {
                string errorMessage = Marshal.PtrToStringAnsi((nint)errorBlob.GetBufferPointer(), (int)errorBlob.GetBufferSize()) ?? "Shader compilation failed.";
                ReleaseComObject(ref errorBlob!);
                throw new InvalidOperationException(errorMessage);
            }

            ReleaseComObject(ref errorBlob!);
            return shaderBlob;
        }
    }

    internal static void ThrowIfFailed(Windows.Win32.Foundation.HRESULT result, string message)
    {
        if (result.Value >= 0)
        {
            return;
        }

        Exception exception = Marshal.GetExceptionForHR(result.Value) ?? new InvalidOperationException(message);
        throw new InvalidOperationException(message, exception);
    }

    internal static void ReleaseComObject<T>(ref T? comObject) where T : class
    {
        if (comObject is null)
        {
            return;
        }

        Marshal.ReleaseComObject(comObject);
        comObject = null;
    }

    internal static void CaptureComInterfacePointer(ref IntPtr interfacePtr, object? comObject, Guid interfaceId)
    {
        ReleaseComPointer(ref interfacePtr);
        if (comObject is not null)
        {
            interfacePtr = GetComInterfacePointer(comObject, interfaceId);
        }
    }

    internal static void ReleaseComPointer(ref IntPtr interfacePtr)
    {
        if (interfacePtr == IntPtr.Zero)
        {
            return;
        }

        Marshal.Release(interfacePtr);
        interfacePtr = IntPtr.Zero;
    }
}
