/* Sidebar Toggler */

function ToggleSideBar() {

	$("body").toggleClass("sidebar-toggled");
	$(".sidebar").toggleClass("toggled");
	if ($(".sidebar").hasClass("toggled")) {
		$('.sidebar .collapse').collapse('hide');
	};

	ResizeActivePlayer();
}

/* Modal Map Functionality */

function LoadBingMap(id, latitude, longitude) {
	var map = new Microsoft.Maps.Map(document.getElementById('bingMap-modal-' + id), {
		center: new Microsoft.Maps.Location(latitude, longitude),
		mapTypeId: Microsoft.Maps.MapTypeId.aerial,
		zoom: 12
	});
	CreateScaledPushpin(map.getCenter(), 'img/hydrophone.png', .25, function (pin) {
		map.entities.push(pin);
	});
}

function CreateScaledPushpin(location, imgUrl, scale, callback) {
	var img = new Image();
	img.onload = function () {
		var c = document.createElement('canvas');
		c.width = img.width * scale;
		c.height = img.height * scale;

		var context = c.getContext('2d');

		//Draw scaled image
		context.drawImage(img, 0, 0, c.width, c.height);

		var pin = new Microsoft.Maps.Pushpin(location, {
			//Generate a base64 image URL from the canvas.
			icon: c.toDataURL(),

			//Anchor based on the center of the image.
			anchor: new Microsoft.Maps.Point(c.width / 2, c.height / 2)
		});

		if (callback) {
			callback(pin);
		}
	};

	img.src = imgUrl;
}

/* Spectrogram Functionality */

var wavesurfer = {}

/* Since resizing skews dimensions of image and player, we'll need to reset each time */

window.onresize = function () {
	ResizeActivePlayer();
}

function ResetPlayButton() {
	$('#play-' + wavesurfer.containerId)
		.removeClass()
		.addClass('fas fa-play-circle fa-3x');
}

function SetPlayButtonToSpinner() {
	$("#play-" + wavesurfer.containerId)
		.removeClass('fa-play-circle')
		.addClass('fa-spinner spinner');
}

function SetSpinnerButtonToPause() {
	$("#play-" + wavesurfer.containerId)
		.removeClass('spinner fa-spinner')
		.addClass('fa-pause-circle');
}

function SetSpinnerButtonToPlay() {
	$("#play-" + wavesurfer.containerId)
		.removeClass('spinner fa-spinner')
		.addClass('fa-play-circle');
}

function SetPlayButtonToPause() {
	$("#play-" + wavesurfer.containerId)
		.removeClass('fa-play-circle')
		.addClass('fa-pause-circle');
}

function SetPauseButtonToPlay() {
	$("#play-" + wavesurfer.containerId)
		.removeClass('fa-pause-circle')
		.addClass('fa-play-circle');
}

function ResetElapsedTime() {
	$("#elapsed-" + wavesurfer.containerId)
		.text('0.00')
}
function SetElapsedTime() {
	$("#elapsed-" + wavesurfer.containerId)
		.text(wavesurfer.getCurrentTime().toFixed(2))
}
function ResetDuration() {
	$("#duration-" + wavesurfer.containerId)
		.text('00.00')
}

function SetDuration() {
	$("#duration-" + wavesurfer.containerId).text(wavesurfer.getDuration().toFixed(2))
}

function SetMaximumVolume() {
	wavesurfer.setVolume(1);
}

function Spectrogram(containerId) {
	var image = $("#spectrogram-" + containerId)[0];

	var width = image.width;
	if (width % 2 != 0) {
		width += 1;
	}

	this.image = image;
	this.height = image.height;
	this.width = width;
}

function AdjustSizes(spectrogram) {
	// This is a funky hack to make the overlay and wavesurfer the correct size
	// When the wave width exceeds the image (should only happen on very large screens)

	if (wavesurfer.drawer.container.clientWidth > spectrogram.width) {
		spectrogram.image.width = wavesurfer.drawer.container.clientWidth;
	}

	wavesurfer.setHeight(spectrogram.image.height);
}

function IsPlayerActive() {
	return (wavesurfer.container != null);
}

function ResizeActivePlayer() {
	if (wavesurfer.container != undefined) {
		spectrogram = new Spectrogram(wavesurfer.containerId);
		AdjustSizes(spectrogram);
	}
}

function DestroyActivePlayer() {

	if (wavesurfer.container != undefined) {
		ResetPlayButton();
		ResetElapsedTime();
		ResetDuration();

		wavesurfer.destroy();

		wavesurfer = {};
	}
}

function InitializeModalSpectrogram(modalId, audioUrl, regionsJson) {

	var containerId = 'modal-' + modalId;

	if (wavesurfer.container != undefined && wavesurfer.containerId != containerId) {
		DestroyActivePlayer();
	}

	var spectrogram = new Spectrogram(containerId);

	wavesurfer = WaveSurfer.create({
		container: '#waveform-' + containerId,
		waveColor: 'rgba(0,0,0,0)',
		progressColor: 'rgba(0,0,0,0)',
		loaderColor: 'purple',
		cursorColor: 'white',
		height: spectrogram.height,
		maxCanvasWidth: spectrogram.width,
		responsive: true,
		fillParent: true,
		plugins: [
			WaveSurfer.regions.create({
				regions: JSON.parse(regionsJson)
			})
		]
	})

	wavesurfer.containerId = containerId;

	SetPlayButtonToSpinner();

	wavesurfer.load(audioUrl);

	wavesurfer.on('ready', function () {
		SetMaximumVolume();
		AdjustSizes(spectrogram);
		SetSpinnerButtonToPlay();
		SetDuration();
	});

	// If the audio fails to load, reset the button so the failure is visible
	// instead of leaving the spinner stuck forever.
	wavesurfer.on('error', function (e) {
		console.error('Spectrogram audio failed to load: ' + e);
		SetSpinnerButtonToPlay();
	});

	// when something is happening, update
	wavesurfer.on('audioprocess', function () {
		SetElapsedTime();
	});

	// when seeking is used
	wavesurfer.on('seek', function () {
		SetElapsedTime();
		SetPlayButtonToPause();
		wavesurfer.play();
	});
}

function ToggleModalSpectrogram() {

	if (wavesurfer.isPlaying()) {
		SetPauseButtonToPlay();
		wavesurfer.pause();
	}
	else {
		SetPlayButtonToPause();
		wavesurfer.play();
	}
}

/* Card Region Preview (shows detector regions on the static spectrogram before playback) */

function PreviewCardRegions(cardId, audioUrl, regionsJson) {

	var regions = JSON.parse(regionsJson || '[]');

	if (regions.length === 0) {
		return;
	}

	var image = document.getElementById('spectrogram-card-' + cardId);
	var waveform = document.getElementById('waveform-card-' + cardId);

	if (image === null || waveform === null || document.getElementById('regions-preview-card-' + cardId) !== null) {
		return;
	}

	// The player draws its own regions; skip the preview while it is active on this card
	if (wavesurfer.containerId == 'card-' + cardId) {
		return;
	}

	// preload="metadata" fetches only the audio header, enough to know the clip duration
	var audio = document.createElement('audio');
	audio.preload = 'metadata';
	audio.src = audioUrl;

	audio.addEventListener('loadedmetadata', function () {

		var duration = audio.duration;

		if (!isFinite(duration) || duration <= 0) {
			return;
		}

		var draw = function () {

			if (wavesurfer.containerId == 'card-' + cardId || document.getElementById('regions-preview-card-' + cardId) !== null) {
				return;
			}

			var preview = document.createElement('div');
			preview.id = 'regions-preview-card-' + cardId;
			preview.style.position = 'relative';
			preview.style.height = image.clientHeight + 'px';

			regions.forEach(function (region) {
				var strip = document.createElement('div');
				strip.className = 'wavesurfer-region';
				strip.style.position = 'absolute';
				strip.style.top = '0';
				strip.style.height = '100%';
				strip.style.left = (region.start / duration * 100) + '%';
				strip.style.width = ((region.end - region.start) / duration * 100) + '%';
				strip.style.backgroundColor = region.color;
				preview.appendChild(strip);
			});

			waveform.parentElement.insertBefore(preview, waveform);
		};

		if (image.complete) {
			draw();
		}
		else {
			image.addEventListener('load', draw, { once: true });
		}
	});
}

function RemoveCardRegionPreview(cardId) {

	var preview = document.getElementById('regions-preview-card-' + cardId);

	if (preview !== null) {
		preview.remove();
	}
}

function CardSpectrogram(cardId, audioUrl, regionsJson) {

	var containerId = 'card-' + cardId;

	if (wavesurfer.container != undefined && wavesurfer.containerId != containerId) {
		DestroyActivePlayer();
	}

	if (wavesurfer.container == undefined) {

		var spectrogram = new Spectrogram(containerId);

		wavesurfer = WaveSurfer.create({
			container: ('#waveform-' + containerId),
			waveColor: 'rgba(0,0,0,0)',
			progressColor: 'rgba(0,0,0,0)',
			loaderColor: 'purple',
			cursorColor: 'white',
			height: spectrogram.height,
			maxCanvasWidth: spectrogram.width,
			responsive: true,
			fillParent: true,
			plugins: [
				WaveSurfer.regions.create({
					regions: JSON.parse(regionsJson || '[]')
				})
			]
		})

		wavesurfer.containerId = containerId;

		SetPlayButtonToSpinner();

		wavesurfer.load(audioUrl);

		wavesurfer.on('ready', function () {
			SetMaximumVolume();
			AdjustSizes(spectrogram);
			SetSpinnerButtonToPause();
			SetDuration();
			// Swap the static region preview for the player's own regions only
			// once they can actually render, so the shades never blink out.
			RemoveCardRegionPreview(cardId);
			wavesurfer.play();
		});

		// If the audio fails to load, reset the button so the failure is visible
		// instead of leaving the spinner stuck forever. The static region
		// preview is intentionally left in place.
		wavesurfer.on('error', function (e) {
			console.error('Spectrogram audio failed to load: ' + e);
			SetSpinnerButtonToPlay();
		});

		// when done playing, reset everything
		wavesurfer.on('finish', function () {
			DestroyActivePlayer();
		});

		// when something is happening, update elapsed time
		wavesurfer.on('audioprocess', function () {
			SetElapsedTime();
		});

		// when seeking is used
		wavesurfer.on('seek', function () {
			SetElapsedTime();
			SetPlayButtonToPause();
			wavesurfer.play();
		});
	}

	else if (wavesurfer.isPlaying()) {
		SetPauseButtonToPlay();
		wavesurfer.pause();
	}

	else {
		SetPlayButtonToPause();
		wavesurfer.play();
	}
}


// Set new default font family and font color to mimic Bootstrap's default styling

function OpenSpectrogramModal(event, anchor) {
	// Ctrl/Cmd/Shift clicks open the details page in a new tab or window instead.
	if (event.ctrlKey || event.metaKey || event.shiftKey) {
		return true;
	}

	DestroyActivePlayer();
	InitializeModalSpectrogram(anchor.dataset.detectionId, anchor.dataset.audioUri, anchor.dataset.regions);
	$(anchor.dataset.modalTarget).modal('show');

	var containerId = 'modal-' + anchor.dataset.detectionId;

	// The player is created while the modal is still hidden, so it measures a
	// zero-width container. If the audio gets ready before the modal fade ends,
	// the waveform and its regions keep that zero width and the shades render
	// invisible. Re-measure and redraw once the modal is actually visible.
	// The containerId check makes sure the player still belongs to this modal
	// in case another one was initialized before this modal finished showing.
	$(anchor.dataset.modalTarget).one('shown.bs.modal', function () {
		if (IsPlayerActive() && wavesurfer.containerId == containerId) {
			ResizeActivePlayer();
			if (wavesurfer.isReady) {
				wavesurfer.drawBuffer();
			}
		}
	});
	return false;
}
