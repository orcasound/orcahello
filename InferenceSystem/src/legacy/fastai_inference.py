import gc
import logging
import os
import shutil
import tempfile
import torch
from fastai.basic_train import load_learner
import pandas as pd
from pydub import AudioSegment
from librosa import get_duration
from pathlib import Path
from numpy import floor
from audio.data import AudioConfig, SpectrogramConfig, AudioList
import torchaudio

# Configure logger for this module
logger = logging.getLogger(__name__)


# Monkey-patch torchaudio.load to avoid torchcodec dependency
# torchaudio 2.9.0+ defaults to torchcodec backend which requires additional installation
# This patch uses soundfile directly which is already installed
_original_torchaudio_load = torchaudio.load

def _patched_torchaudio_load(filepath, *args, **kwargs):
    """Wrapper for torchaudio.load that uses soundfile directly instead of torchcodec"""
    import soundfile as sf
    import torch as t
    
    # Load audio using soundfile
    data, samplerate = sf.read(str(filepath), dtype='float32')
    
    # Convert to torch tensor and ensure shape is (channels, samples)
    waveform = t.from_numpy(data.T if data.ndim > 1 else data.reshape(1, -1))
    
    return waveform, samplerate

torchaudio.load = _patched_torchaudio_load


# Monkey-patch torchaudio.save to avoid torchcodec dependency
# torchaudio 2.9.0+ defaults to torchcodec backend which requires additional installation
# This patch uses soundfile directly which is already installed
_original_torchaudio_save = torchaudio.save

def _patched_torchaudio_save(filepath, src, sample_rate, *args, **kwargs):
    """Wrapper for torchaudio.save that uses soundfile directly instead of torchcodec"""
    import soundfile as sf
    import numpy as np
    
    # Convert torch tensor to numpy array
    # src is expected to be (channels, samples), soundfile expects (samples, channels)
    audio_data = src.numpy().T if src.ndim > 1 else src.numpy().reshape(-1, 1)
    
    # Save audio using soundfile
    sf.write(str(filepath), audio_data, sample_rate)

torchaudio.save = _patched_torchaudio_save


# Monkey-patch torch.load to use weights_only=False for compatibility with fastai models
# PyTorch 2.6+ changed the default to weights_only=True for security, but fastai models
# require weights_only=False to load functools.partial and other objects
_original_torch_load = torch.load

def _patched_torch_load(*args, **kwargs):
    """Wrapper for torch.load that defaults to weights_only=False for fastai compatibility"""
    if 'weights_only' not in kwargs:
        kwargs['weights_only'] = False
    return _original_torch_load(*args, **kwargs)

torch.load = _patched_torch_load


def load_model(mPath, mName="stg2-rn18.pkl"):
    return load_learner(mPath, mName)


def get_wave_file(wav_file):
    '''
    Function to load a wav file
    '''
    return AudioSegment.from_wav(wav_file)


def export_wave_file(audio, begin, end, dest):
    '''
    Function to extract a smaller wav file based start and end duration information
    '''
    sub_audio = audio[begin * 1000:end * 1000]
    sub_audio.export(dest, format="wav")


def extract_segments(audioPath, sampleDict, destnPath, suffix):
    '''
    Function to extract segments given an audio path folder and proposal segments
    '''
    # Listing the local audio files
    local_audio_files = str(audioPath) + '/'
    for wav_file in sampleDict.keys():
        audio_file = get_wave_file(local_audio_files + wav_file)
        for begin_time, end_time in sampleDict[wav_file]:
            output_file_name = wav_file.lower().replace(
                '.wav', '') + '_' + str(begin_time) + '_' + str(
                    end_time) + suffix + '.wav'
            output_file_path = destnPath + output_file_name
            export_wave_file(audio_file, begin_time,
                             end_time, output_file_path)


class FastAIModel():
    def __init__(self, model_path, model_name="stg2-rn18.pkl", threshold=0.5, min_num_positive_calls_threshold=3, batch_size=32, use_gpu=False, smooth_predictions=True):
        self.model = load_model(model_path, model_name)
        self.threshold = threshold
        self.min_num_positive_calls_threshold = min_num_positive_calls_threshold
        self.batch_size = batch_size
        self.use_gpu = use_gpu
        self.smooth_predictions = smooth_predictions  # If False, skip rolling average smoothing
        
        # Move model to appropriate device once during initialization
        if self.use_gpu and torch.cuda.is_available():
            self.model.model.cuda()
        else:
            self.model.model.cpu()

    def predict(self, wav_file_path):
        '''
        Function which generates local predictions using wavefile
        '''

        # Creates local directory to save 2 second clips
        # local_dir = "./fastai_dir/"
        local_dir = tempfile.mkdtemp()+"/"
        if os.path.exists(local_dir):
            shutil.rmtree(local_dir, ignore_errors=False, onerror=None)
            os.makedirs(local_dir)
        else:
            os.makedirs(local_dir)

        try:
            # Infer clip length
            max_length = get_duration(path=wav_file_path)
            print(os.path.basename(wav_file_path))
            print("Length of Audio Clip:{0}".format(max_length))
            #max_length = 60
            # Generating 2 sec proposal with 1 sec hop length
            twoSecList = []
            for i in range(int(floor(max_length)-1)):
                twoSecList.append([i, i+2])

            # Creating a proposal dictionary
            two_sec_dict = {}
            two_sec_dict[Path(wav_file_path).name] = twoSecList

            # Creating 2 sec segments from the defined wavefile using proposals built above.
            # "use_a_real_wavname.wav" will generate -> "use_a_real_wavname_1_3.wav", "use_a_real_wavname_2_4.wav" etc. files in fastai_dir folder
            extract_segments(
                str(Path(wav_file_path).parent),
                two_sec_dict,
                local_dir,
                ""
            )
        
            # Defining Audio config needed to create on the fly mel spectograms
            config = AudioConfig(standardize=False,
                                 sg_cfg=SpectrogramConfig(
                                     f_min=0.0,  # Minimum frequency to Display
                                     f_max=10000,  # Maximum Frequency to Display
                                     hop_length=256,
                                     n_fft=2560,  # Number of Samples for Fourier
                                     n_mels=256,  # Mel bins
                                     pad=0,
                                     to_db_scale=True,  # Converting to DB scale
                                     top_db=100,  # Top decibel sound
                                     win_length=None,
                                     n_mfcc=20)
                                 )
            config.duration = 4000  # 4 sec padding or snip
            config.resample_to = 20000  # Every sample at 20000 frequency
            config.downmix=True
            config.pad_mode = "zeros-after"  # Make deterministic: zeros at end only

            # Creating an Audio DataLoader
            test_data_folder = Path(local_dir)
            tfms = None
            test = AudioList.from_folder(
                test_data_folder, config=config).split_none().label_empty()
            testdb = test.transform(tfms).databunch(bs=self.batch_size)

            # Scoring each audio segment
            pathList = list(pd.Series(test_data_folder.ls()).astype('str'))
            
            # Per-item prediction using fastai's predict() which handles
            # model.eval() and torch.no_grad() internally
            # Note: FastAI's predict() also handles device placement for inputs internally
            predictions = []
            for item in testdb.x:
                predictions.append(self.model.predict(item)[2][1])

            # Explicitly clean up large fastai objects to encourage immediate memory release
            del test
            del testdb
            gc.collect()
            if self.use_gpu and torch.cuda.is_available():
                torch.cuda.empty_cache()

            # Aggregating predictions

            # Creating a DataFrame
            prediction = pd.DataFrame({'FilePath': pathList, 'confidence': predictions})

            # Converting prediction to float
            prediction['confidence'] = prediction.confidence.astype(float)

            # Extracting Starting time from file name
            prediction['start_time_s'] = prediction.FilePath.apply(lambda x: int(x.split('_')[-2]))

            # Sorting the file based on start_time_s
            prediction = prediction.sort_values(
                ['start_time_s']).reset_index(drop=True)

            if self.smooth_predictions:
                # Rolling Window (to average at per second level)
                submission = pd.DataFrame(
                        {
                            'wav_filename': Path(wav_file_path).name,
                            'duration_s': 1.0,
                            'confidence': list(prediction.rolling(2)['confidence'].mean().values)
                        }
                    ).reset_index().rename(columns={'index': 'start_time_s'})

                # Updating first row
                submission.loc[0, 'confidence'] = prediction.confidence[0]

                # Adding last row
                lastLine = pd.DataFrame({
                    'wav_filename': Path(wav_file_path).name,
                    'start_time_s': [submission.start_time_s.max()+1],
                    'duration_s': 1.0,
                    'confidence': [prediction.confidence[prediction.shape[0]-1]]
                    })
                submission = pd.concat([submission, lastLine], ignore_index=True)
                submission = submission[['wav_filename', 'start_time_s', 'duration_s', 'confidence']]
            else:
                # No smoothing - use raw per-segment predictions directly
                submission = pd.DataFrame({
                    'wav_filename': Path(wav_file_path).name,
                    'start_time_s': prediction['start_time_s'],
                    'duration_s': 2.0,  # Each segment is 2 seconds
                    'confidence': prediction['confidence']
                })

            # Initialize output JSON
            result_json = {}
            result_json = dict(
                submission=submission,
                local_predictions=list((submission['confidence'] > self.threshold).astype(int)),
                local_confidences=list(submission['confidence'])
            )

            result_json['global_prediction'] = int(sum(result_json["local_predictions"]) >= self.min_num_positive_calls_threshold)
            result_json['global_confidence'] = submission.loc[(submission['confidence'] > self.threshold), 'confidence'].mean()*100
            if pd.isnull(result_json["global_confidence"]):
                result_json["global_confidence"] = 0

            return result_json
        finally:
            # Clean temp folder - log any cleanup failures instead of silently ignoring
            try:
                shutil.rmtree(local_dir)
            except Exception as e:
                logger.warning(f"Failed to clean up temp directory {local_dir}: {e}")
