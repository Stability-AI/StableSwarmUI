from http.server import HTTPServer, BaseHTTPRequestHandler
import sys, time, json, base64
from pathlib import Path
from io import BytesIO
from transformers import AutoProcessor, AutoModel
from PIL import Image
import torch


################ Logging ################
LOG_TEXT = ""

def log(text):
    global LOG_TEXT
    LOG_TEXT += text + "\n"
    print(text)


################ Core ################
DEVICE = "cuda"
DTYPE = torch.float16

class Scorer():
    def load(self):
        raise NotImplementedError()
    def unload(self):
        raise NotImplementedError()
    def calculate(self, prompt, images):
        raise NotImplementedError()


################ PickScore ################
class PickScore(Scorer):
    processor = None
    model = None

    ################ Model loading & prep ################
    def load(self):
        if self.model:
            self.model = self.model.to(DEVICE).to(dtype=DTYPE)
            return
        processor_name_or_path = "laion/CLIP-ViT-H-14-laion2B-s32B-b79K"
        model_pretrained_name_or_path = "yuvalkirstain/PickScore_v1"
        self.processor = AutoProcessor.from_pretrained(processor_name_or_path)
        self.model = AutoModel.from_pretrained(model_pretrained_name_or_path).eval().to(DEVICE)

    def unload(self):
        if self.model:
            self.model = self.model.to('cpu')

    ################ Actual PickScore handler ################
    def calculate(self, prompt, images):
        image_inputs = self.processor(images=images, padding=True, truncation=True, max_length=77, return_tensors="pt").to(DEVICE).to(DTYPE)
        text_inputs = self.processor(text=prompt, padding=True, truncation=True, max_length=77, return_tensors="pt").to(DEVICE).to(DTYPE)
        with torch.no_grad(), torch.autocast(DEVICE, DTYPE):
            # embed
            image_embs = self.model.get_image_features(**image_inputs)
            image_embs = image_embs / torch.norm(image_embs, dim=-1, keepdim=True)
            text_embs = self.model.get_text_features(**text_inputs)
            text_embs = text_embs / torch.norm(text_embs, dim=-1, keepdim=True)
            # score
            scores = (text_embs @ image_embs.T)[0]
            print(f"PickScore raw value {scores.cpu().tolist()}")
            calc = 0.3 / (scores + 0.18)
            calc2 = 1 - calc * calc * calc
            scores = (calc2 - 0.2) * 1.9
        return scores.cpu().tolist()


################ Christoph Schuhmann's Aesthetic Predictors ################
class aesth_scorer(Scorer):
    model = None

    def __init__(self, model_id, min, scale):
        self.model_id = model_id
        self.min = min
        self.scale = scale

    def load(self):
        if self.model:
            self.model.to(DEVICE)
            self.model.to(DTYPE)
            return
        if not Path(self.model_id).exists():
            url = f"https://github.com/christophschuhmann/improved-aesthetic-predictor/blob/fe88a163f4661b4ddabba0751ff645e2e620746e/{self.model_id}?raw=true"
            import requests
            r = requests.get(url)
            with open(self.model_id, "wb") as f:
                f.write(r.content)
        from christoph_aesthetic import AestheticPredictor
        self.model = AestheticPredictor()
        self.model.load(self.model_id, DEVICE)

    def unload(self):
        if self.model:
            self.model.to('cpu')
    
    def correct(self, score):
        return (score - self.min) / self.scale

    def calculate(self, prompt, images):
        scores = [self.correct(self.model.predict(img)[0][0].tolist()) for img in images]
        return scores


################ Instances ################
pickScore = PickScore()
schuhmannClipMlp = aesth_scorer("sac+logos+ava1-l14-linearMSE.pth", 1, 7)

def by_name(name):
    if name == 'pickscore':
        return pickScore
    elif name == 'schuhmann_clip_plus_mlp':
        return schuhmannClipMlp
    else:
        raise NotImplementedError(f'No scorer with name {name}')


################ Web Handler ################
class Handler(BaseHTTPRequestHandler):
    def good_response(self, data):
        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(data).encode('utf-8'))

    def do_POST(self):
        length = int(self.headers.get('content-length'))
        message = json.loads(self.rfile.read(length))
        if self.path == '/API/Ping':
            self.good_response({'result': 'success'})
        elif self.path == '/API/DoScore':
            global LOG_TEXT
            scorer = None
            try:
                scorer = by_name(message['scorer'])
                scorer.load()
                t_before = time.time()
                imgs = [Image.open(BytesIO(base64.b64decode(message['image']))).convert('RGB')]
                score = max(0, min(1, scorer.calculate(message['prompt'], imgs)[0]))
                t_after = time.time()
                max_mem = torch.cuda.max_memory_allocated()
                log(f"allocated max mem: {max_mem / 1024 / 1024 / 1024:.3f} GiB, took {t_after - t_before:.3f} seconds, yielded value {score}")
                torch.cuda.empty_cache()
                self.good_response({'result': score, 'log': LOG_TEXT})
                LOG_TEXT = ''
            except Exception as ex:
                self.good_response({'error': f'failed: {ex}', 'log': f"{ex}\n{LOG_TEXT}"})
                LOG_TEXT = ''
                raise
            if scorer:
                scorer.unload()
        else:
            self.send_response(404)
            self.end_headers()
            self.wfile.write(json.dumps({'error': 'bad route'}).encode('utf-8'))

    def do_GET(self):
        self.send_response(404)
        self.end_headers()
        self.wfile.write(b'Invalid request - this is a POST only internal server')


################ Init/execute ################
def run(port):
    server_address = ('', port)
    httpd = HTTPServer(server_address, Handler)
    log(f'Running on port {port}')
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        exit(0)

run(int(sys.argv[1]))
