import torch, comfy
from nodes import MAX_RESOLUTION


class SwarmClipTextEncodeAdvanced:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "clip": ("CLIP", ),
                "steps": ("INT", {"default": 20, "min": 1, "max": 10000}),
                "prompt": ("STRING", {"multiline": True, "dynamicPrompts": True} ),
                "width": ("INT", {"default": 1024.0, "min": 0, "max": MAX_RESOLUTION}),
                "height": ("INT", {"default": 1024.0, "min": 0, "max": MAX_RESOLUTION}),
                "target_width": ("INT", {"default": 1024.0, "min": 0, "max": MAX_RESOLUTION}),
                "target_height": ("INT", {"default": 1024.0, "min": 0, "max": MAX_RESOLUTION}),
            }
        }

    CATEGORY = "StableSwarmUI/clip"
    RETURN_TYPES = ("CONDITIONING",)
    FUNCTION = "encode"

    def encode(self, clip, steps: int, prompt: str, width: int, height: int, target_width: int, target_height: int):

        encoding_cache = {}

        def text_to_cond(text: str, start_percent: float, end_percent: float):
            text = text.replace("\0\1", "[").replace("\0\2", "]")
            if text in encoding_cache:
                cond, pooled = encoding_cache[text]
            else:
                tokens = clip.tokenize(text)
                cond, pooled = clip.encode_from_tokens(tokens, return_pooled=True)
                encoding_cache[text] = (cond, pooled)
            return [cond, {"pooled_output": pooled, "width": width, "height": height, "crop_w": 0, "crop_h": 0, "target_width": target_width, "target_height": target_height, "start_percent": start_percent, "end_percent": end_percent}]

        prompt = prompt.replace("\\[", "\0\1").replace("\\]", "\0\2")

        remaining = prompt
        chunks = []
        any = False
        while True:
            start = remaining.find("[")
            if start == -1:
                chunks.append({'text': remaining, 'type': 'text'})
                break
            end = remaining.find("]", start)
            if end == -1:
                chunks[-1].text += remaining
                break
            chunks.append({'text': remaining[:start], 'type': 'text'})
            control = remaining[start + 1:end]
            ctrltype = 'raw'
            data = control
            piped = control.split("|")
            coloned = control.split(":")
            if len(piped) > 1:
                data = piped
                ctrltype = 'pipe'
                any = True
            elif len(coloned) == 3:
                when = float(coloned[2])
                if when < 1:
                    when = when * steps
                data = { 'before': coloned[0], 'after': coloned[1], 'when': when }
                ctrltype = 'coloned'
                any = True
            elif len(coloned) == 2:
                when = float(coloned[1])
                if when < 1:
                    when = when * steps
                data = { 'before': '', 'after': coloned[0], 'when': when }
                ctrltype = 'coloned'
                any = True
            chunks.append({'text': control, 'data': data, 'type': ctrltype})
            remaining = remaining[end + 1:]

        print(chunks)

        if not any:
            return ([text_to_cond(prompt, 0, 1)], )

        conds_out = []
        last_text = ""
        start_perc = 0
        for i in range(steps):
            perc = i / steps
            text = ""
            for chunk in chunks:
                if chunk['type'] == 'text':
                    text += chunk['text']
                else:
                    if chunk['type'] == 'pipe':
                        text += chunk['data'][i % len(chunk['data'])]
                    elif chunk['type'] == 'coloned':
                        if i >= chunk['data']['when']:
                            text += chunk['data']['after']
                        else:
                            text += chunk['data']['before']
                    else:
                        text += chunk['data']
            if text != last_text or i == 0:
                if i != 0:
                    conds_out.append(text_to_cond(last_text, start_perc - 0.001, perc + 0.001))
                last_text = text
                start_perc = perc
        conds_out.append(text_to_cond(last_text, start_perc - 0.001, 1))
        return (conds_out, )


NODE_CLASS_MAPPINGS = {
    "SwarmClipTextEncodeAdvanced": SwarmClipTextEncodeAdvanced,
}
