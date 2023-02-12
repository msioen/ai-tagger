# https://learn.microsoft.com/en-us/azure/machine-learning/v1/how-to-deploy-advanced-entry-script

from azureml.contrib.services.aml_request import AMLRequest, rawhttp
from azureml.contrib.services.aml_response import AMLResponse
import os
import logging
import json
import torch
from PIL import Image
from donut import DonutModel

def init():
    """
    This function is called when the container is initialized/started, typically after create/update of the deployment.
    You can write the logic here to perform init operations like caching the model in memory
    """
    global pretrained_model
    # AZUREML_MODEL_DIR is an environment variable created during deployment.
    # It is the path to the model folder (./azureml-models/$MODEL_NAME/$VERSION)
    # Please provide your model's folder name if there is one
    model_path = os.path.join(
        os.getenv("AZUREML_MODEL_DIR"), "model/donut-base-finetuned-rvlcdip"
    )
    
    pretrained_model = DonutModel.from_pretrained(model_path)

    if torch.cuda.is_available():
        logging.info("cuda is available")
        pretrained_model.half()
        device = torch.device("cuda")
        pretrained_model.to(device)

    pretrained_model.eval()

    logging.info("Init complete")

@rawhttp
def run(request):
    """
    This function is called for every invocation of the endpoint to perform the actual scoring/prediction.
    """
    logging.info("request received")

    if request.method == 'POST':
        file_bytes = request.files["image"]
        input_img = Image.open(file_bytes).convert('RGB')

        logging.info("image parsed")
        logging.info(input_img.size)

        task_prompt = "<s_rvlcdip>"
        result = pretrained_model.inference(image=input_img, prompt=task_prompt)["predictions"][0]

        logging.info("Request processed")
        logging.info(result)

        return AMLResponse(json.dumps(result), 200)
    else:
        return AMLResponse("Unsupported HTTP Method", 500)
