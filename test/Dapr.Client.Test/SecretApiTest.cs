﻿// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Grpc.Core;
    using Moq;
    using Xunit;
    using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

    public class SecretApiTest
    {
        [Fact]
        public async Task GetSecretAsync_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.GetSecretAsync("testStore", "test_key", metadata);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.GetSecretRequest>();
            envelope.StoreName.Should().Be("testStore");
            envelope.Key.Should().Be("test_key");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task GetSecretAsync_ReturnSingleSecret()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.GetSecretAsync("testStore", "test_key", metadata);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.GetSecretRequest>();
            envelope.StoreName.Should().Be("testStore");
            envelope.Key.Should().Be("test_key");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>
            {
                { "redis_secret", "Guess_Redis" }
            };
            var secretsResponse = await SendResponseWithSecrets(secrets, request);

            // Get response and validate
            secretsResponse.Count.Should().Be(1);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"].Should().Be("Guess_Redis");
        }

        [Fact]
        public async Task GetSecretAsync_WithSlashesInName()
        {
            await using var client = TestClient.CreateForDaprClient();

            var request = await client.CaptureGrpcRequestAsync(async DaprClient =>
            {
                return await DaprClient.GetSecretAsync("testStore", "us-west-1/org/xpto/secretabc");
            });

            request.Dismiss();

            //Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.GetSecretRequest>();
            envelope.StoreName.Should().Be("testStore");
            envelope.Key.Should().Be("us-west-1/org/xpto/secretabc");

            var secrets = new Dictionary<string, string> { { "us-west-1/org/xpto/secretabc", "abc123" } };
            var secretsResponse = await SendResponseWithSecrets(secrets, request);

            //Get response and validate
            secretsResponse.Count.Should().Be(1);
            secretsResponse.ContainsKey("us-west-1/org/xpto/secretabc").Should().BeTrue();
            secretsResponse["us-west-1/org/xpto/secretabc"].Should().Be("abc123");
        }

        [Fact]
        public async Task GetSecretAsync_ReturnMultipleSecrets()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.GetSecretAsync("testStore", "test_key", metadata);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.GetSecretRequest>();
            envelope.StoreName.Should().Be("testStore");
            envelope.Key.Should().Be("test_key");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>
            {
                { "redis_secret", "Guess_Redis" },
                { "kafka_secret", "Guess_Kafka" }
            };
            var secretsResponse = await SendResponseWithSecrets(secrets, request);

            // Get response and validate
            secretsResponse.Count.Should().Be(2);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"].Should().Be("Guess_Redis");
            secretsResponse.ContainsKey("kafka_secret").Should().BeTrue();
            secretsResponse["kafka_secret"].Should().Be("Guess_Kafka");
        }

        [Fact]
        public async Task GetSecretAsync_WithCancelledToken()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await client.InnerClient.GetSecretAsync("testStore", "test_key", metadata, cancellationToken: cts.Token);
            });
        }

        [Fact]
        public async Task GetSecretAsync_WrapsRpcException()
        {
            var client = new MockClient();

            var rpcStatus = new Grpc.Core.Status(StatusCode.Internal, "not gonna work");
            var rpcException = new RpcException(rpcStatus, new Metadata(), "not gonna work");

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.GetSecretAsync(It.IsAny<Autogen.Grpc.v1.GetSecretRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await client.DaprClient.GetSecretAsync("test", "test");
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        [Fact]
        public async Task GetBulkSecretAsync_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.GetBulkSecretAsync("testStore", metadata);
            });

            request.Dismiss();

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.GetBulkSecretRequest>();
            envelope.StoreName.Should().Be("testStore");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");
        }

        [Fact]
        public async Task GetBulkSecretAsync_ReturnSingleSecret()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.GetBulkSecretAsync("testStore", metadata);
            });

            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.GetBulkSecretRequest>();
            envelope.StoreName.Should().Be("testStore");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>();
            secrets.Add("redis_secret", "Guess_Redis");
            var secretsResponse = await SendBulkResponseWithSecrets(secrets, request);

            // Get response and validate
            secretsResponse.Count.Should().Be(1);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"]["redis_secret"].Should().Be("Guess_Redis");
        }

        [Fact]
        public async Task GetBulkSecretAsync_ReturnMultipleSecrets()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");

            var request = await client.CaptureGrpcRequestAsync(async daprClient =>
            {
                return await daprClient.GetBulkSecretAsync("testStore", metadata);
            });


            // Get Request and validate
            var envelope = await request.GetRequestEnvelopeAsync<Autogenerated.GetBulkSecretRequest>();
            envelope.StoreName.Should().Be("testStore");
            envelope.Metadata.Count.Should().Be(2);
            envelope.Metadata.Keys.Contains("key1").Should().BeTrue();
            envelope.Metadata.Keys.Contains("key2").Should().BeTrue();
            envelope.Metadata["key1"].Should().Be("value1");
            envelope.Metadata["key2"].Should().Be("value2");

            // Create Response & Respond
            var secrets = new Dictionary<string, string>();
            secrets.Add("redis_secret", "Guess_Redis");
            secrets.Add("kafka_secret", "Guess_Kafka");
            var secretsResponse = await SendBulkResponseWithSecrets(secrets, request);

            // Get response and validate
            secretsResponse.Count.Should().Be(2);
            secretsResponse.ContainsKey("redis_secret").Should().BeTrue();
            secretsResponse["redis_secret"]["redis_secret"].Should().Be("Guess_Redis");
            secretsResponse.ContainsKey("kafka_secret").Should().BeTrue();
            secretsResponse["kafka_secret"]["kafka_secret"].Should().Be("Guess_Kafka");
        }

        [Fact]
        public async Task GetBulkSecretAsync_WithCancelledToken()
        {
            await using var client = TestClient.CreateForDaprClient();

            var metadata = new Dictionary<string, string>();
            metadata.Add("key1", "value1");
            metadata.Add("key2", "value2");

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await client.InnerClient.GetBulkSecretAsync("testStore", metadata, cancellationToken: cts.Token);
            });
        }

        [Fact]
        public async Task GetBulkSecretAsync_WrapsRpcException()
        {
            var client = new MockClient();

            var rpcStatus = new Grpc.Core.Status(StatusCode.Internal, "not gonna work");
            var rpcException = new RpcException(rpcStatus, new Metadata(), "not gonna work");

            // Setup the mock client to throw an Rpc Exception with the expected details info
            client.Mock
                .Setup(m => m.GetBulkSecretAsync(It.IsAny<Autogen.Grpc.v1.GetBulkSecretRequest>(), It.IsAny<CallOptions>()))
                .Throws(rpcException);

            var ex = await Assert.ThrowsAsync<DaprException>(async () => 
            {
                await client.DaprClient.GetBulkSecretAsync("test");
            });
            Assert.Same(rpcException, ex.InnerException);
        }

        private async Task<TResponse> SendResponseWithSecrets<TResponse>(Dictionary<string, string> secrets, TestClient<DaprClient>.TestGrpcRequest<TResponse> request)
        {
            var secretResponse = new Autogenerated.GetSecretResponse();
            secretResponse.Data.Add(secrets);

            return await request.CompleteWithMessageAsync(secretResponse);
        }

        private async Task<TResponse> SendBulkResponseWithSecrets<TResponse>(Dictionary<string, string> secrets, TestClient<DaprClient>.TestGrpcRequest<TResponse> request)
        {
            var getBulkSecretResponse = new Autogenerated.GetBulkSecretResponse();
            foreach (var secret in secrets)
            {
                var secretsResponse = new Autogenerated.SecretResponse();
                secretsResponse.Secrets[secret.Key] = secret.Value;
                getBulkSecretResponse.Data.Add(secret.Key, secretsResponse);
            }

            return await request.CompleteWithMessageAsync(getBulkSecretResponse);
        }
    }
}
