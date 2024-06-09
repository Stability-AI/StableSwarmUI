from PIL import Image
import numpy as np
from server import PromptServer, BinaryEventTypes
import io, struct, subprocess, os, random, sys, time
from imageio_ffmpeg import get_ffmpeg_exe
import folder_paths

VIDEO_ID = 12346
FFMPEG_PATH = get_ffmpeg_exe()


class SwarmSaveAnimationWS:
    methods = {"default": 4, "fastest": 0, "slowest": 6}

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "images": ("IMAGE", ),
                "fps": ("FLOAT", {"default": 6.0, "min": 0.01, "max": 1000.0, "step": 0.01}),
                "lossless": ("BOOLEAN", {"default": True}),
                "quality": ("INT", {"default": 80, "min": 0, "max": 100}),
                "method": (list(s.methods.keys()),),
                "format": (["webp", "gif", "h264-mp4", "webm", "prores"],),
            },
        }

    CATEGORY = "StableSwarmUI/video"
    RETURN_TYPES = ()
    FUNCTION = "save_images"
    OUTPUT_NODE = True

    def save_images(self, images, fps, lossless, quality, method, format):
        method = self.methods.get(method)

        out_img = io.BytesIO()
        if format in ["webp", "gif"]:
            if format == "webp":
                type_num = 3
            else:
                type_num = 4
            pil_images = []
            for image in images:
                i = 255. * image.cpu().numpy()
                img = Image.fromarray(np.clip(i, 0, 255).astype(np.uint8))
                pil_images.append(img)
            pil_images[0].save(out_img, save_all=True, duration=int(1000.0 / fps), append_images=pil_images[1 : len(pil_images)], lossless=lossless, quality=quality, method=method, format=format.upper(), loop=0)
        else:
            i = 255. * images.cpu().numpy()
            raw_images = np.clip(i, 0, 255).astype(np.uint8)
            args = [FFMPEG_PATH, "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
                    "-s", f"{len(raw_images[0][0])}x{len(raw_images[0])}", "-r", str(fps), "-i", "-", "-n" ]
            if format == "h264-mp4":
                args += ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "19"]
                ext = "mp4"
                type_num = 5
            elif format == "webm":
                args += ["-pix_fmt", "yuv420p", "-crf", "23"]
                ext = "webm"
                type_num = 6
            elif format == "prores":
                args += ["-c:v", "prores_ks", "-profile:v", "3", "-pix_fmt", "yuv422p10le"]
                ext = "mov"
                type_num = 7
            path = folder_paths.get_save_image_path("swarm_tmp_", folder_paths.get_temp_directory())[0]
            rand = '%016x' % random.getrandbits(64)
            file = os.path.join(path, f"swarm_tmp_{rand}.{ext}")
            result = subprocess.run(args + [file], input=raw_images.tobytes(), capture_output=True, check=True)
            if result.stderr:
                print(result.stderr.decode("utf-8"), file=sys.stderr)
            # TODO: Is there a way to get ffmpeg to operate entirely in memory?
            with open(file, "rb") as f:
                out_img.write(f.read())
            os.remove(file)

        out = io.BytesIO()
        header = struct.pack(">I", type_num)
        out.write(header)
        out.write(out_img.getvalue())
        out.seek(0)
        preview_bytes = out.getvalue()
        server = PromptServer.instance
        server.send_sync("progress", {"value": 12346, "max": 12346}, sid=server.client_id)
        server.send_sync(BinaryEventTypes.PREVIEW_IMAGE, preview_bytes, sid=server.client_id)

        return { }

    @classmethod
    def IS_CHANGED(s, images, fps, lossless, quality, method, format):
        return time.time()


NODE_CLASS_MAPPINGS = {
    "SwarmSaveAnimationWS": SwarmSaveAnimationWS,
}
