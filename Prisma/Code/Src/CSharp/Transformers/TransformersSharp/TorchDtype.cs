namespace TransformersSharp
{
    internal class TorchDtypeAttribute : Attribute
    {
        public string Dtype { get; }
        public TorchDtypeAttribute(string dtype)
        {
            Dtype = dtype;
        }
    }

    /// <summary>
    /// Represents PyTorch data types for tensors.
    /// See https://pytorch.org/docs/stable/tensor_attributes.html for more information.
    /// </summary>
    public enum TorchDtype
    {
        /// <summary>
        /// 32-bit floating point.
        /// </summary>
        [TorchDtypeAttribute("float32")]
        Float32,
        
        /// <summary>
        /// 64-bit floating point.
        /// </summary>
        [TorchDtypeAttribute("float64")]
        Float64,
        
        /// <summary>
        /// 64-bit complex number.
        /// </summary>
        [TorchDtypeAttribute("complex64")]
        Complex64,
        
        /// <summary>
        /// 128-bit complex number.
        /// </summary>
        [TorchDtypeAttribute("complex128")]
        Complex128,
        
        /// <summary>
        /// 16-bit floating point.
        /// </summary>
        [TorchDtypeAttribute("float16")]
        Float16,
        
        /// <summary>
        /// 16-bit brain floating point.
        /// </summary>
        [TorchDtypeAttribute("bfloat16")]
        BFloat16,
        
        /// <summary>
        /// 8-bit unsigned integer.
        /// </summary>
        [TorchDtypeAttribute("uint8")]
        UInt8,
        
        /// <summary>
        /// 8-bit signed integer.
        /// </summary>
        [TorchDtypeAttribute("int8")]
        Int8,
        
        /// <summary>
        /// 16-bit signed integer.
        /// </summary>
        [TorchDtypeAttribute("int16")]
        Int16,
        
        /// <summary>
        /// 32-bit signed integer.
        /// </summary>
        [TorchDtypeAttribute("int32")]
        Int32,
        
        /// <summary>
        /// 64-bit signed integer.
        /// </summary>
        [TorchDtypeAttribute("int64")]
        Int64,
        
        /// <summary>
        /// Boolean type.
        /// </summary>
        [TorchDtypeAttribute("bool")]
        Bool
    }
}
