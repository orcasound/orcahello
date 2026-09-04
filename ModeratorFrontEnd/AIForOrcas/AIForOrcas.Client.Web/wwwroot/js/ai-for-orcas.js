/* Candidate card layout on small landscape screens */

// Stacked at the card top (evidence first); once the details have scrolled into the
// top part of the screen the card body splits side by side, spectrogram pinned at
// half width beside the scrolling details. The distance the details move on a
// switch is compensated on the scroll position, so the content under the finger
// stays put. One passive scroll listener, one rAF-throttled pass over the cards.
var cardLayoutScheduled = false;
var cardLayoutWatching = false;
var landscapeSmallScreen = window.matchMedia('(orientation: landscape) and (min-width: 568px) and (max-width: 991.98px)');
// Of the viewport height, from the top. Two different lines so the layout can
// never flip back and forth at one scroll position: split when the details come
// up to 30%, stack again only once the row (the details' top edge in the split
// state) is back down past 45%.
var SPLIT_WHEN_DETAILS_WITHIN = 0.3;
var STACK_WHEN_DETAILS_BELOW = 0.45;

function UpdateCardLayout() {

	cardLayoutScheduled = false;

	var shift = 0;


	document.querySelectorAll('.detection-spectrogram').forEach(function (column) {
		var card = column.closest('.card');
		var row = column.parentElement;
		var details = row.querySelector('.detection-details');

		if (!card || !details) {
			return;
		}

		var split = card.classList.contains('detection-split');
		var wanted = split;

		if (!landscapeSmallScreen.matches) {
			wanted = false;
		} else if (!split && details.getBoundingClientRect().top <= window.innerHeight * SPLIT_WHEN_DETAILS_WITHIN) {
			wanted = true;
		} else if (split && row.getBoundingClientRect().top > window.innerHeight * STACK_WHEN_DETAILS_BELOW) {
			wanted = false;
		}

		if (wanted != split) {
			// Keep the content where the finger is: compensate the scroll by how far
			// the details column moved, not by the row height (the narrower column
			// also gets taller, so the two differ). A rotation or a resize switches
			// every card in one pass, so the shifts are summed and applied once at
			// the end; a card entirely below the fold moves nothing the moderator
			// can see and is left out of the sum.
			var before = details.getBoundingClientRect().top;
			card.classList.toggle('detection-split', wanted);
			var after = details.getBoundingClientRect().top;
			if (before < window.innerHeight) {
				shift += after - before;
			}
			ResizeActivePlayer();

			// Lets the css fade the full-width spectrogram back in
			window.clearTimeout(card.stackingTimer);
			card.classList.toggle('detection-stacking', !wanted);
			if (!wanted) {
				card.stackingTimer = window.setTimeout(function () { card.classList.remove('detection-stacking'); }, 700);
			}
		}
	});

	if (shift) {
		window.scrollBy(0, shift);
	}
}

// How much of the screen a pinned column is holding, so anything the browser scrolls
// to (a tab to the next field, an autofocus, a restored position) can clear it
// instead of landing underneath. The height only settles once the spectrogram image
// has loaded, so watch the element rather than measuring once.
var pinnedHeightObserver = null;

function WatchPinnedHeight() {

	var column = document.querySelector('.detection-spectrogram');
	if (!column || !window.ResizeObserver) {
		return;
	}

	var publish = function () {
		var height = getComputedStyle(column).position === 'sticky' ? Math.round(column.offsetHeight) : 0;
		document.documentElement.style.setProperty('--pinned-height', height + 'px');
	};

	if (pinnedHeightObserver) {
		pinnedHeightObserver.disconnect();
	}
	pinnedHeightObserver = new ResizeObserver(publish);
	pinnedHeightObserver.observe(column);
	publish();
}

function ScheduleCardLayout() {

	if (!cardLayoutScheduled) {
		cardLayoutScheduled = true;
		window.requestAnimationFrame(UpdateCardLayout);
	}
}

// Called after every card render; wires the listeners once and refreshes the state.
function WatchCardLayout() {

	if (!cardLayoutWatching) {
		cardLayoutWatching = true;
		window.addEventListener('scroll', ScheduleCardLayout, { passive: true });
		window.addEventListener('resize', ScheduleCardLayout);
		window.addEventListener('resize', WatchPinnedHeight);
	}

	WatchPinnedHeight();
	ScheduleCardLayout();
}

/* Sidebar Toggler */

// Below the md breakpoint the topbar hamburger is the way to open the sidebar, so
// it always starts collapsed there and the content gets the full width. On a larger
// screen the moderator's last choice is remembered instead.
var smallScreen = window.matchMedia('(max-width: 767.98px)');
var sidebarPreferenceKey = 'orcahello.sidebar-collapsed';

function ReadSideBarPreference() {
	try {
		return window.localStorage.getItem(sidebarPreferenceKey);
	} catch (e) {
		return null;   // private mode, or storage blocked
	}
}

function WriteSideBarPreference(collapsed) {
	try {
		window.localStorage.setItem(sidebarPreferenceKey, collapsed ? '1' : '0');
	} catch (e) {
		// nothing to do, the sidebar just will not remember
	}
}

// True when the sidebar should be collapsed for the viewport we are in now.
function SideBarShouldCollapse() {
	return smallScreen.matches || ReadSideBarPreference() === '1';
}

function ApplySideBarState(collapsed) {
	$("body").toggleClass("sidebar-toggled", collapsed);
	$(".sidebar").toggleClass("toggled", collapsed);
	if (collapsed) {
		$('.sidebar .collapse').collapse('hide');
	}
}

function CollapseSideBarOnSmallScreens() {

	ApplySideBarState(SideBarShouldCollapse());

	// Crossing the breakpoint re-decides: into the small range it collapses, back
	// out of it the remembered choice applies again.
	var onBreakpoint = function () {
		ApplySideBarState(SideBarShouldCollapse());
	};

	if (smallScreen.addEventListener) {
		smallScreen.addEventListener('change', onBreakpoint);
	} else {
		smallScreen.addListener(onBreakpoint);   // Safari before 14
	}

	// On a small screen the open sidebar covers the page, so a tap on a nav item
	// would navigate behind it. Close it on the way out. Only for links that leave
	// the page: the accordion togglers (href="#") and the external ones (target)
	// keep it open. Namespaced and re-bound so repeated renders leave one handler.
	$(document).off("click.sidebarnav").on("click.sidebarnav", ".sidebar a", function () {
		var href = $(this).attr("href");
		if (!smallScreen.matches || !href || href.charAt(0) === "#" || $(this).attr("target")) {
			return;
		}
		ApplySideBarState(true);
	});
}

// This script loads before Blazor renders, so the body class goes on now and the
// sidebar paints in its final state from the first frame (the css rule on
// body.sidebar-toggled .sidebar); CollapseSideBarOnSmallScreens then puts the
// sidebar's own class in step after the first render.
if (SideBarShouldCollapse()) {
	document.body.classList.add('sidebar-toggled');
}

function ToggleSideBar() {

	var collapsed = !$(".sidebar").hasClass("toggled");
	ApplySideBarState(collapsed);

	// The phone is always collapsed on arrival, so only a real screen records a
	// preference; otherwise a desktop choice would follow the moderator to the phone.
	if (!smallScreen.matches) {
		WriteSideBarPreference(collapsed);
	}

	ResizeActivePlayer();
}

/* Back to top */

// sb-admin drives this control with a queued fadeIn/fadeOut per scroll event, so a
// fast scroll on a phone leaves the queue running behind the finger and the control
// visible when it should not be (or the other way round). Decide it from the current
// scroll position on every frame instead; the css keys off the body class.
(function () {

	var pending = false;

	var update = function () {
		pending = false;
		var top = window.pageYOffset || document.documentElement.scrollTop || 0;
		document.body.classList.toggle('show-scroll-to-top', top > 100);
	};

	var onScroll = function () {
		if (!pending) {
			pending = true;
			window.requestAnimationFrame(update);
		}
	};

	window.addEventListener('scroll', onScroll, { passive: true });
	window.addEventListener('resize', onScroll, { passive: true });
	document.addEventListener('DOMContentLoaded', update);
	update();

	// sb-admin animates the jump with jQuery easing (easeInOutExpo), and that plugin
	// is not among the scripts this app loads, so its handler throws and the control
	// does nothing when tapped. Take the click first and scroll natively. Registered
	// before sb-admin's, so stopping propagation keeps the broken one from running.
	document.addEventListener('click', function (event) {
		var control = event.target.closest ? event.target.closest('.scroll-to-top') : null;
		if (!control) {
			return;
		}
		event.preventDefault();
		event.stopImmediatePropagation();
		window.scrollTo({ top: 0, behavior: 'smooth' });
	});
})();

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

// After a submit re-renders the list, open the next candidate at its card top,
// spectrogram first, instead of wherever the previous card left the scroll.
function ScrollCardIntoView(detectionId) {

	var image = document.getElementById('spectrogram-card-' + detectionId);
	var card = image ? image.closest('.card') : null;

	if (card) {
		// A landscape card lands stacked, evidence first (class set by UpdateCardLayout).
		card.classList.remove('detection-split');
		card.scrollIntoView({ block: 'start' });
	}
}

// Touch on a card spectrogram that has no player yet: create the player and
// start playback at the touched point, like the modal spectrogram does. When
// this card's player already exists, wavesurfer's own click-to-seek on the
// waveform handles the touch, so do nothing here.
function CardSpectrogramTouch(event, overlay) {

	var containerId = 'card-' + overlay.dataset.detectionId;

	if (wavesurfer.container != undefined && wavesurfer.containerId == containerId) {
		return;
	}

	var rect = overlay.getBoundingClientRect();
	var fraction = (event.clientX - rect.left) / rect.width;

	CardSpectrogram(overlay.dataset.detectionId, overlay.dataset.audioUri, fraction);
}

function CardSpectrogram(cardId, audioUrl, startFraction) {

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
			// A touch created this player carrying where it landed; start
			// playback from that point (seekTo takes a 0..1 progress). The
			// seek handler below already calls play(), so calling it again
			// here would start a second buffer source.
			if (startFraction > 0) {
				wavesurfer.seekTo(startFraction);
			}
			else {
				wavesurfer.play();
			}
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
