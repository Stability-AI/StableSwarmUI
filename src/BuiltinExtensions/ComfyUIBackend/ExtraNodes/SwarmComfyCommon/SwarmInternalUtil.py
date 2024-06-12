import comfy, folder_paths, execution

# This is purely a hack to provide a list of embeds in the object_info report.
# Code referenced from Comfy VAE impl. Probably does nothing useful in an actual workflow.
class SwarmEmbedLoaderListProvider:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "embed_name": (folder_paths.get_filename_list("embeddings"), )
            }
        }

    CATEGORY = "StableSwarmUI/internal"
    RETURN_TYPES = ("EMBEDDING",)
    FUNCTION = "load_embed"

    def load_embed(self, embed_name):
        embed_path = folder_paths.get_full_path("embedding", embed_name)
        sd = comfy.utils.load_torch_file(embed_path)
        return (sd,)


NODE_CLASS_MAPPINGS = {
    "SwarmEmbedLoaderListProvider": SwarmEmbedLoaderListProvider,
}
