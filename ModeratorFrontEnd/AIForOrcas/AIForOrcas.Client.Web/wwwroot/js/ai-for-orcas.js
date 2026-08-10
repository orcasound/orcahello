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
		// Redraw now instead of waiting for wavesurfer's debounced responsive
		// redraw, so the progress cursor tracks the scaling image during a
		// drag. Same event the debounced handler fires, which redraws the
		// waveform and repositions the progress cursor.
		if (wavesurfer.isReady) {
			wavesurfer.drawer.fireEvent('redraw');
		}
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

function InitializeModalSpectrogram(modalId, audioUrl) {

	var containerId = 'modal-' + modalId;

	if (wavesurfer.container != undefined && wavesurfer.containerId != containerId) {
		DestroyActivePlayer();
	}

	var spectrogram = new Spectrogram(containerId);

	// The detector shades are drawn over the spectrogram image by
	// DrawRegionShades; the player only contributes the progress cursor
	// and the audio itself, so no regions plugin is needed.
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
		// Clip instead of scroll: a window drag can briefly leave the canvas
		// wider than its container, blinking a scrollbar under the spectrogram.
		hideScrollbar: true
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

/* Region Shades (detector regions drawn over the spectrogram image) */

// The shades are the single source of truth for where the AI heard something:
// percentage-positioned strips over the responsive spectrogram image, in both
// the card and its modal, present before, during, and after playback. The
// player never draws its own regions, so the shades cannot jump, blink, or
// disappear across the playback lifecycle.
function DrawRegionShades(detectionId, audioUrl, regionsJson) {

	var regions = JSON.parse(regionsJson || '[]');

	if (regions.length === 0) {
		return;
	}

	// Blazor re-renders the component many times; once every present container
	// has its shades there is nothing left to draw, so skip the metadata probe.
	var needsShades = function (containerId) {
		return document.getElementById('spectrogram-' + containerId) !== null
			&& document.getElementById('regions-shades-' + containerId) === null;
	};

	if (!needsShades('card-' + detectionId) && !needsShades('modal-' + detectionId)) {
		return;
	}

	var drawInto = function (containerId, duration) {

		var image = document.getElementById('spectrogram-' + containerId);
		var waveform = document.getElementById('waveform-' + containerId);

		if (image === null || waveform === null || document.getElementById('regions-shades-' + containerId) !== null) {
			return;
		}

		var draw = function () {

			if (document.getElementById('regions-shades-' + containerId) !== null) {
				return;
			}

			// Absolutely positioned over the card-img-overlay, which tracks the
			// responsive spectrogram image, so the strips follow every resize
			// and the waveform below is not displaced out of the overlay.
			var shades = document.createElement('div');
			shades.id = 'regions-shades-' + containerId;
			shades.style.position = 'absolute';
			shades.style.top = '0';
			shades.style.left = '0';
			shades.style.width = '100%';
			shades.style.height = '100%';

			regions.forEach(function (region) {
				var strip = document.createElement('div');
				strip.className = 'wavesurfer-region';
				strip.style.position = 'absolute';
				strip.style.top = '0';
				strip.style.height = '100%';
				strip.style.left = (region.start / duration * 100) + '%';
				strip.style.width = ((region.end - region.start) / duration * 100) + '%';
				strip.style.backgroundColor = region.color;
				shades.appendChild(strip);
			});

			// Inserted below the waveform so the progress cursor stays visible
			// and seek clicks keep landing on the player.
			waveform.parentElement.insertBefore(shades, waveform);
		};

		if (image.complete) {
			draw();
		}
		else {
			image.addEventListener('load', draw, { once: true });
		}
	};

	var drawForDuration = function (duration) {

		if (!isFinite(duration) || duration <= 0) {
			return;
		}

		drawInto('card-' + detectionId, duration);
		drawInto('modal-' + detectionId, duration);
	};

	// preload="metadata" fetches only the audio header, enough to know the clip duration
	var audio = document.createElement('audio');
	audio.preload = 'metadata';
	audio.src = audioUrl;

	audio.addEventListener('loadedmetadata', function () {
		drawForDuration(audio.duration);
	});

	// If the audio cannot load at all, still show where the AI heard something.
	// Every deployment produces 60 second clips (inference_segment_size), so a
	// fixed fallback keeps the strips close to their real place until playback
	// becomes available again.
	audio.addEventListener('error', function () {
		drawForDuration(60);
	});
}

function CardSpectrogram(cardId, audioUrl) {

	var containerId = 'card-' + cardId;

	if (wavesurfer.container != undefined && wavesurfer.containerId != containerId) {
		DestroyActivePlayer();
	}

	if (wavesurfer.container == undefined) {

		var spectrogram = new Spectrogram(containerId);

		// The detector shades are drawn over the spectrogram image by
		// DrawRegionShades; the player only contributes the progress cursor
		// and the audio itself, so no regions plugin is needed.
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
			// Same as the modal player: no scrollbar blink on resize.
			hideScrollbar: true
		})

		wavesurfer.containerId = containerId;

		SetPlayButtonToSpinner();

		wavesurfer.load(audioUrl);

		wavesurfer.on('ready', function () {
			SetMaximumVolume();
			AdjustSizes(spectrogram);
			SetSpinnerButtonToPause();
			SetDuration();
			wavesurfer.play();
		});

		// If the audio fails to load, reset the button so the failure is visible
		// instead of leaving the spinner stuck forever. The region shades stay
		// in place either way.
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
	InitializeModalSpectrogram(anchor.dataset.detectionId, anchor.dataset.audioUri);
	$(anchor.dataset.modalTarget).modal('show');

	var containerId = 'modal-' + anchor.dataset.detectionId;

	// The player is created while the modal is still hidden, so it measures a
	// zero-width container. If the audio gets ready before the modal fade ends,
	// the waveform keeps that zero width and the progress cursor renders
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
