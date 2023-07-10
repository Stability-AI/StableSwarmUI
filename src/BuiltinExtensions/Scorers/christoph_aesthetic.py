### Based on https://github.com/christophschuhmann/improved-aesthetic-predictor/blob/main/simple_inference.py
### Under Apache license 2.0

import torch
import pytorch_lightning as pl
import torch.nn as nn
import clip
import numpy as np

class MLP(pl.LightningModule):
    def __init__(self, input_size, xcol='emb', ycol='avg_rating'):
        super().__init__()
        self.input_size = input_size
        self.xcol = xcol
        self.ycol = ycol
        self.layers = nn.Sequential(
            nn.Linear(self.input_size, 1024),
            nn.Dropout(0.2),
            nn.Linear(1024, 128),
            nn.Dropout(0.2),
            nn.Linear(128, 64),
            nn.Dropout(0.1),
            nn.Linear(64, 16),
            nn.Linear(16, 1)
        )

    def forward(self, x):
        return self.layers(x)

def normalized(a, axis=-1, order=2):
    l2 = np.atleast_1d(np.linalg.norm(a, order, axis))
    l2[l2 == 0] = 1
    return a / np.expand_dims(l2, axis)

class AestheticPredictor():
    model = None
    model2 = None

    def to(self, dev):
        self.model.to(dev)
        self.model2.to(dev)

    def load(self, name, device):
        self.model = MLP(768)  # CLIP embedding dim is 768 for CLIP ViT L 14
        s = torch.load(name)
        self.model.load_state_dict(s)
        self.model.eval().to(device)
        self.model2, self.preprocess = clip.load("ViT-L/14", device=device)  #RN50x64
        self.model2.eval().to(device)

    def predict(self, img):
        image = self.preprocess(img).unsqueeze(0).to(self.model.device).to(self.model.dtype)
        with torch.no_grad(), torch.autocast('cuda', self.model.dtype):
            image_features = self.model2.encode_image(image)
            im_emb_arr = normalized(image_features.cpu().detach().numpy())
            prediction = self.model(torch.from_numpy(im_emb_arr).to(self.model.device).type(torch.cuda.FloatTensor))
            return prediction.cpu().detach().numpy()
