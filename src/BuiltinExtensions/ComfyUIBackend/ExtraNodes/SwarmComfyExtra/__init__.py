from . import SwarmRemBg, SwarmSaveAnimationWS, SwarmYolo

NODE_CLASS_MAPPINGS = (
    SwarmRemBg.NODE_CLASS_MAPPINGS
    | SwarmSaveAnimationWS.NODE_CLASS_MAPPINGS
    | SwarmYolo.NODE_CLASS_MAPPINGS
)
