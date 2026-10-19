#include <gst/gst.h>
#include <gst/app/gstappsink.h>
#include <gst/video/video.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

static int packet(const uint8_t *pixels, int width, int height, int stride) {
    uint32_t header[] = {GUINT32_TO_LE(width), GUINT32_TO_LE(height), GUINT32_TO_LE(width * height * 4)};
    if (fwrite(header, sizeof(header), 1, stdout) != 1) return 0;
    for (int y = 0; y < height; y++)
        if (fwrite(pixels + y * stride, width * 4, 1, stdout) != 1) return 0;
    return fflush(stdout) == 0;
}
int main(int argc, char **argv) {
    if (argc == 2 && strcmp(argv[1], "--self-test") == 0) {
        uint8_t pixels[16] = {0,0,255,255,0,255,0,255,255,0,0,255,255,255,255,255};
        return packet(pixels, 2, 2, 8) ? 0 : 1;
    }
    if (argc == 2 && strcmp(argv[1], "--self-test-denied") == 0) {
        fprintf(stderr, "Camera access denied. Check your device permissions.\n"); return 1;
    }
    if (argc == 2 && strcmp(argv[1], "--self-test-unavailable") == 0) {
        fprintf(stderr, "No camera is connected.\n"); return 1;
    }
    if (argc != 2 || strcmp(argv[1], "--capture") != 0) return 2;
    gst_init(NULL, NULL);
    GError *error = NULL;
    GstElement *pipeline = gst_parse_launch("autovideosrc ! videoconvert ! videoscale ! videorate ! video/x-raw,format=BGRA,width=640,height=480,framerate=15/1 ! appsink name=frames max-buffers=1 drop=true sync=false", &error);
    if (!pipeline || error) {
        fprintf(stderr, "Camera unavailable: %s\n", error ? error->message : "missing GStreamer plugins");
        if (error) g_error_free(error);
        if (pipeline) gst_object_unref(pipeline);
        return 1;
    }
    GstElement *sink = gst_bin_get_by_name(GST_BIN(pipeline), "frames");
    GstBus *bus = gst_element_get_bus(pipeline);
    int code = 0;
    if (gst_element_set_state(pipeline, GST_STATE_PLAYING) == GST_STATE_CHANGE_FAILURE) {
        fprintf(stderr, "Camera permission denied or device unavailable.\n"); code = 1;
    }
    while (!code) {
        GstSample *sample = gst_app_sink_try_pull_sample(GST_APP_SINK(sink), GST_SECOND);
        if (!sample) {
            GstMessage *message = gst_bus_pop_filtered(bus, GST_MESSAGE_ERROR | GST_MESSAGE_EOS);
            if (message) {
                if (GST_MESSAGE_TYPE(message) == GST_MESSAGE_ERROR) {
                    GError *failure = NULL; gchar *debug = NULL;
                    gst_message_parse_error(message, &failure, &debug);
                    fprintf(stderr, "Camera unavailable: %s\n", failure->message);
                    g_error_free(failure); g_free(debug); code = 1;
                }
                gst_message_unref(message); break;
            }
            continue;
        }
        GstVideoInfo info; GstVideoFrame frame;
        if (gst_video_info_from_caps(&info, gst_sample_get_caps(sample)) &&
            gst_video_frame_map(&frame, &info, gst_sample_get_buffer(sample), GST_MAP_READ)) {
            int ok = packet(GST_VIDEO_FRAME_PLANE_DATA(&frame, 0), GST_VIDEO_FRAME_WIDTH(&frame), GST_VIDEO_FRAME_HEIGHT(&frame), GST_VIDEO_FRAME_PLANE_STRIDE(&frame, 0));
            gst_video_frame_unmap(&frame);
            if (!ok) code = 1;
        } else code = 1;
        gst_sample_unref(sample);
    }
    gst_element_set_state(pipeline, GST_STATE_NULL);
    gst_object_unref(bus); gst_object_unref(sink); gst_object_unref(pipeline);
    return code;
}
