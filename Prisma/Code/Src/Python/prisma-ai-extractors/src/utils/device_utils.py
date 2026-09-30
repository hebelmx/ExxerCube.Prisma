"""
Device and hardware configuration utilities.
"""

import torch
from typing import Dict, Any


def is_cuda_supported() -> bool:
    """
    Check if CUDA is properly supported on this system.
    
    Returns:
        bool: True if CUDA is available and functional
    """
    if not torch.cuda.is_available():
        return False
    
    try:
        count = torch._C._cuda_getDeviceCount()
        if count == 0:
            return False
        # Check device capability (Pascal+ for modern features)
        major, _ = torch.cuda.get_device_capability()
        return major >= 6  # Pascal+ (GTX 10xx and newer)
    except Exception:
        return False


def get_optimal_device_config() -> Dict[str, Any]:
    """
    Get optimal device configuration for the current hardware.
    
    Returns:
        Dictionary with:
            - device: torch.device object
            - dtype: optimal dtype for the hardware
            - attn_impl: attention implementation to use
            - cuda_available: whether CUDA is available
            - gpu_name: GPU name if available
    """
    config = {
        'cuda_available': False,
        'gpu_name': None,
        'attn_impl': None
    }
    
    has_cuda = is_cuda_supported()
    
    if has_cuda:
        config['cuda_available'] = True
        config['device'] = torch.device("cuda")
        
        # Get GPU info
        config['gpu_name'] = torch.cuda.get_device_name(0)
        major, minor = torch.cuda.get_device_capability()
        
        # Select optimal dtype based on GPU generation
        if major >= 8:  # Ampere and newer (RTX 30xx+)
            config['dtype'] = torch.bfloat16
            config['attn_impl'] = "flash_attention_2"
        elif major >= 7:  # Turing/Volta (RTX 20xx, V100)
            config['dtype'] = torch.float16
            config['attn_impl'] = "sdpa"
        else:  # Pascal and older
            config['dtype'] = torch.float16
            config['attn_impl'] = "sdpa"
            
        # Check for flash attention availability
        try:
            import flash_attn  # noqa: F401
            if major < 8:
                # Flash attention requires Ampere+
                config['attn_impl'] = "sdpa"
        except ImportError:
            config['attn_impl'] = "sdpa"
    else:
        config['device'] = torch.device("cpu")
        config['dtype'] = torch.float32
        config['attn_impl'] = None
    
    return config


def get_memory_info() -> Dict[str, Any]:
    """
    Get memory information for the current device.
    
    Returns:
        Dictionary with memory statistics
    """
    info = {}
    
    if torch.cuda.is_available():
        info['gpu_memory_allocated'] = torch.cuda.memory_allocated() / 1024**3  # GB
        info['gpu_memory_reserved'] = torch.cuda.memory_reserved() / 1024**3  # GB
        info['gpu_memory_total'] = torch.cuda.get_device_properties(0).total_memory / 1024**3  # GB
    
    try:
        import psutil
        info['cpu_memory_percent'] = psutil.virtual_memory().percent
        info['cpu_memory_available'] = psutil.virtual_memory().available / 1024**3  # GB
    except ImportError:
        pass
    
    return info