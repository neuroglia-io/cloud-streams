<p align="center">
  <img src="assets/logo-typing.png" height="350px" alt="Logo"/>
</p>
<hr>

Cloud Streams is a cloud-native tool that empowers users to capture and process [cloud events](https://cloudevents.io/) in real-time, enabling event-driven architectures that are both scalable and efficient. With Cloud Streams, you can customize how you stream, filter, partition, and mutate the [cloud events](https://cloudevents.io/) you consume using declarative rules, giving you full control over how your data is processed and consumed.

As event-driven architectures have become more popular, so too have the challenges associated with building and managing them, especially when dealing with large volumes of data. This is where Cloud Streams comes in. This powerful and straightforward tool provides a simple and effective way to capture, process, and route cloud events in real-time, allowing you to create event-driven architectures that are both flexible and reliable.

## Features

- **Streaming**: Cloud Streams allows users to stream cloud events in real-time, ensuring that all relevant data is captured and processed as soon as it becomes available. This feature is especially useful for applications that require real-time data processing, such as financial trading or IoT applications.
- **Filtering**: Cloud Streams provides powerful filtering capabilities, allowing users to filter cloud events based on declarative rules. This ensures that only relevant data is captured and processed, reducing processing time and improving overall system performance.
- **Persistence**: Cloud Streams ensures that all consumed cloud events are persisted, guaranteeing that data is not lost and will eventually be delivered to the appropriate consumers. This feature is especially useful for mission-critical applications where data loss is not acceptable.
- **Routing**: Cloud Streams provides flexible routing capabilities, allowing users to route cloud events based on declarative rules. This ensures that data is sent to the correct consumers, reducing data processing errors and improving system efficiency.
- **Mutations**: Cloud Streams offers the capability to modify incoming events prior to sending them to subscribers, which allows for backward compatibility and the ability to remove or transform sensitive data.
- **Fault Tolerance**: Cloud Streams provides fault tolerance by implementing atomic, configurable retry policies that enable brokers to resend missed events to a consumer that was previously unavailable or that responded with a non-success status code. This ensures that all missed events are eventually received by the consumer in the order they were originally posted. The retry policy enables potential errors to be fixed at the consumer level, without the risk of losing data.
- **Playback**: Cloud Streams provides playback capabilities, allowing users to play back previously captured cloud events. This can be useful for debugging, testing, or catastrophic recovery purposes. With this feature, users can recover from system failures or data corruption by replaying previously captured events.

## Benefits

- **Scalability**: Cloud Streams is designed to be highly scalable, allowing users to process large volumes of data with ease. This makes it an ideal solution for applications with rapidly changing data requirements.
- **Flexibility**: Cloud Streams provides flexible filtering and routing capabilities, allowing users to customize the way their data is processed and consumed. This ensures that users can adapt to changing data requirements quickly and easily.
- **Efficiency**: Cloud Streams' filtering and routing capabilities improve data processing efficiency, reducing the time and resources required to process data. This can lead to significant cost savings over time.
- **Reliability**: Cloud Streams is designed to be highly reliable, ensuring that data is captured, processed, and consumed accurately and efficiently. This makes it an ideal solution for mission-critical applications.
- **Historical Data Access**: Cloud Streams provides historical data access capabilities, allowing users to access previously captured cloud events. This can be useful for analyzing historical data, as well as for replaying events for debugging, testing, or catastrophic recovery purposes. With this feature, users can recover from system failures or data corruption by accessing previously captured events.
- **Easy to Integrate**: Cloud Streams is extremely easy to integrate in existing solutions. As a matter of fact, producers can publish events to Cloud Streams simply by POSTing Cloud Events, using the structured mode, to a specific gateway. Consumers, on the other hand, only need to have an endpoint that can accept cloud events sent over HTTP using the structured mode. That's it. All the configuration, and control, is offloaded to Cloud Streams resources such as subscriptions.

## Solution Overview

<img src="assets/solution-overview.png" alt="Solution overview"/>
