import torch

# This code copied from https://github.com/comfyanonymous/ComfyUI_experiments/blob/master/reference_only.py
# And modified to work better in Swarm generated workflows

class SwarmReferenceOnly:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "model": ("MODEL",),
                "reference": ("LATENT",),
                "latent": ("LATENT",)
            }
        }

    CATEGORY = "StableSwarmUI/sampling"
    RETURN_TYPES = ("MODEL", "LATENT")
    FUNCTION = "reference_only"

    def reference_only(self, model, reference, latent):
        model_reference = model.clone()
        reference["samples"] = torch.nn.functional.interpolate(reference["samples"], size=(latent["samples"].shape[2], latent["samples"].shape[3]), mode="bilinear")

        batch = latent["samples"].shape[0] + reference["samples"].shape[0]
        def reference_apply(q, k, v, extra_options):
            k = k.clone().repeat(1, 2, 1)
            offset = 0
            if q.shape[0] > batch:
                offset = batch

            for o in range(0, q.shape[0], batch):
                for x in range(1, batch):
                    k[x + o, q.shape[1]:] = q[o,:]

            return q, k, k

        model_reference.set_model_attn1_patch(reference_apply)
        out_latent = torch.cat((reference["samples"], latent["samples"]))
        if "noise_mask" in latent:
            mask = latent["noise_mask"]
        else:
            mask = torch.ones((64,64), dtype=torch.float32, device="cpu")

        if len(mask.shape) < 3:
            mask = mask.unsqueeze(0)
        if mask.shape[0] < latent["samples"].shape[0]:
            mask = mask.repeat(latent["samples"].shape[0], 1, 1)

        out_mask = torch.zeros((1,mask.shape[1],mask.shape[2]), dtype=torch.float32, device="cpu")
        return (model_reference, {"samples": out_latent, "noise_mask": torch.cat((out_mask, mask))})

NODE_CLASS_MAPPINGS = {
    "SwarmReferenceOnly": SwarmReferenceOnly,
}
