using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Types;
using static VRCFaceTracking.Core.Params.Expressions.UnifiedExpressions;
using Tobii.StreamEngine;

public class VRCFT_TobiiHTCVive : ExtTrackingModule
{
    // What your interface is able to send as tracking data.
    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, false);

    // Initialise the variables needed for Tobii Stream Engine
    private IntPtr apiContext;
    private IntPtr deviceContext;
    private List<string>? urls;
    private tobii_error_t result;

    // This is the first function ran by VRCFaceTracking. Make sure to completely initialize 
    // your tracking interface or the data to be accepted by VRCFaceTracking here. This will let 
    // VRCFaceTracking know what data is available to be sent from your tracking interface at initialization.
    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        var state = (eyeAvailable, expressionAvailable);

        ModuleInformation.Name = "VRCFT Tobii HTC Vive Devkit";

        // Example of an embedded image stream being referenced as a stream
        var stream = 
            GetType()
            .Assembly
            .GetManifestResourceStream("ExampleExtTrackingInterface.Assets.x-logo.png");

        // Setting the stream to be referenced by VRCFaceTracking.
        ModuleInformation.StaticImages = 
            stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        //... Initialising module. Modify state tuple as needed (or use bool contexts to determine what should be initialized).
        Logger.LogInformation("Initialising module...");

        // Create Tobii API
        result = Interop.tobii_api_create(out apiContext, null);
        if (result == tobii_error_t.TOBII_ERROR_NO_ERROR)
            Logger.LogInformation("Tobii API Create Success");
        else
        {
            Logger.LogCritical("Tobii API Create Failure!");
            return (false, false);
        }

        // Enumerate devices to find connected eye trackers
        result = Interop.tobii_enumerate_local_device_urls(apiContext, out urls);
        if (result == tobii_error_t.TOBII_ERROR_NO_ERROR)
            Logger.LogInformation("Tobii Enumerate Devices Success");
        else
        {
            Logger.LogCritical("Tobii Enumerate Devices Failure!");
            return (false, false);
        }
        if (urls.Count == 0)
        {
            Logger.LogCritical("No Tobii Device found");
            return (false, false);
        }
        Logger.LogInformation("Tobii Devices found:");
        foreach (string url in urls)
        {
            Logger.LogInformation(url);
        }

        // Connect to the first tracker found
        result = Interop.tobii_device_create(apiContext, urls[0], Interop.tobii_field_of_use_t.TOBII_FIELD_OF_USE_INTERACTIVE, out deviceContext);
        if (result == tobii_error_t.TOBII_ERROR_NO_ERROR)
            Logger.LogInformation("Tobii Device Create Success");
        else
        {
            Logger.LogCritical("Tobii Device Create Failure!");
            return (false, false);
        }

        return state;
    }

    // Polls data from the tracking interface.
    // VRCFaceTracking will run this function in a separate thread;
    public override void Update()
    {
        // Subscribe to consumer data which will be sent to the classes local ProcessCallback method
        result = Interop.tobii_wearable_consumer_data_subscribe(deviceContext, ProcessCallback);
        if (result == tobii_error_t.TOBII_ERROR_NO_ERROR)
            Logger.LogInformation("Tobii Wearable Consumer Data Subscribe Success");
        else
        {
            Logger.LogCritical("Tobii Wearable Consumer Data Subscribe Failure!");
            return;
        }

        if (Status == ModuleState.Active) // Module Status validation
        {
            Interop.tobii_wait_for_callbacks(new[] { deviceContext });
            Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR || result == tobii_error_t.TOBII_ERROR_TIMED_OUT);

            // Process callbacks on this thread if data is available
            Interop.tobii_device_process_callbacks(deviceContext);
            Debug.Assert(result == tobii_error_t.TOBII_ERROR_NO_ERROR);
        }

        // Add a delay or halt for the next update cycle for performance. eg: 
        Thread.Sleep(10);
    }

    private void ProcessCallback(ref tobii_wearable_consumer_data_t consumerData, IntPtr userData)
    {
        if (consumerData.left.pupil_position_in_sensor_area_validity == tobii_validity_t.TOBII_VALIDITY_VALID)
            UnifiedTracking.Data.Eye.Left.Openness = consumerData.left.blink == tobii_state_bool_t.TOBII_STATE_BOOL_TRUE ? (float)0 : (float)1;

        if (consumerData.right.pupil_position_in_sensor_area_validity == tobii_validity_t.TOBII_VALIDITY_VALID)
            UnifiedTracking.Data.Eye.Right.Openness = consumerData.right.blink == tobii_state_bool_t.TOBII_STATE_BOOL_TRUE ? (float)0 : (float)1;
    }

    // Called when the module is unloaded or VRCFaceTracking itself tears down.
    public override void Teardown()
    {
        //... Deinitialize tracking interface; dispose any data created with the module.
        Interop.tobii_wearable_consumer_data_unsubscribe(deviceContext);
        Interop.tobii_device_destroy(deviceContext);
        Interop.tobii_api_destroy(apiContext);
    }
}



