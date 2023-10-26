# This entire file is copied from ComfyUI and is just a micro-hack to avoid a dependency on PyTorch Lightning
import pickle

load = pickle.load

class Empty:
    pass

class Unpickler(pickle.Unpickler):
    def find_class(self, module, name):
        if module.startswith("pytorch_lightning"):
            return Empty
        return super().find_class(module, name)
