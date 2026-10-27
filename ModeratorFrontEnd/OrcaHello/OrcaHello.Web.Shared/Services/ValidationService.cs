// ---------------------------------------------------------------
// RESTFulSense - .NET Core Library

// Copyright (c) 2022 Hassan Habib All rights reserved.

// Material in this repository is made available under the following terms:
//  1.Code is licensed under the MIT license, reproduced below.
//  2. Documentation is licensed under the Creative Commons Attribution 3.0 United States (Unported) License.
//     The text of the license can be found here: http://creativecommons.org/licenses/by/3.0/legalcode

// The MIT License(MIT)

// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all copies or substantial 
// portions of the Software.

// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT 
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES 
// OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN 
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// This is an abbreviated version of the code originally provided here: 
// https://github.com/hassanhabib/RESTFulSense/blob/master/RESTFulSense
// and
// https://github.com/hassanhabib/Xeption
// ---------------------------------------------------------------
using System.Collections;
using static OrcaHello.Web.Shared.Services.HttpResponseNotFoundException;

namespace OrcaHello.Web.Shared.Services
{
    [ExcludeFromCodeCoverage]
    public class ValidationService
    {
        public async static ValueTask ValidateHttpResponseAsync(HttpResponseMessage httpResponseMessage)
        {
            string content = await httpResponseMessage.Content.ReadAsStringAsync();
            bool isProblemDetailContent = IsProblemDetail(content);

            switch (isProblemDetailContent)
            {
                case true when httpResponseMessage.StatusCode == HttpStatusCode.BadRequest:
                    ValidationProblemDetails badRequestDetails = MapToProblemDetails(content);
                    throw new HttpResponseBadRequestException(httpResponseMessage, badRequestDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.BadRequest:
                    throw new HttpResponseBadRequestException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized:
                    ValidationProblemDetails UnauthorizedDetails = MapToProblemDetails(content);
                    throw new HttpResponseUnauthorizedException(httpResponseMessage, UnauthorizedDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized:
                    throw new HttpResponseUnauthorizedException(httpResponseMessage, content);

                case false when NotFoundWithNoContent(httpResponseMessage):
                    throw new HttpResponseUrlNotFoundException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.NoContent:
                    throw new HttpResponseNotFoundException(httpResponseMessage, MapToProblemDetails(content));

                case false when httpResponseMessage.StatusCode == HttpStatusCode.NoContent:
                    throw new HttpResponseNotFoundException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.NotFound:
                    ValidationProblemDetails NotFoundDetails = MapToProblemDetails(content);
                    throw new HttpResponseNotFoundException(httpResponseMessage, NotFoundDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.NotFound:
                    throw new HttpResponseNotFoundException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError:
                    ValidationProblemDetails InternalServerErrorDetails = MapToProblemDetails(content);
                    throw new HttpResponseInternalServerErrorException(httpResponseMessage, InternalServerErrorDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError:
                    throw new HttpResponseInternalServerErrorException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.Conflict:
                    ValidationProblemDetails ConflictDetails = MapToProblemDetails(content);
                    throw new HttpResponseConflictException(httpResponseMessage, ConflictDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.Conflict:
                    throw new HttpResponseConflictException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.Forbidden:
                    ValidationProblemDetails ForbiddenDetails = MapToProblemDetails(content);
                    throw new HttpResponseForbiddenException(httpResponseMessage, ForbiddenDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.Forbidden:
                    throw new HttpResponseForbiddenException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.MethodNotAllowed:
                    ValidationProblemDetails MethodNotAllowedDetails = MapToProblemDetails(content);
                    throw new HttpResponseMethodNotAllowedException(httpResponseMessage, MethodNotAllowedDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.MethodNotAllowed:
                    throw new HttpResponseMethodNotAllowedException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.NotAcceptable:
                    ValidationProblemDetails NotAcceptableDetails = MapToProblemDetails(content);
                    throw new HttpResponseNotAcceptableException(httpResponseMessage, NotAcceptableDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.NotAcceptable:
                    throw new HttpResponseNotAcceptableException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.NotImplemented:
                    ValidationProblemDetails NotImplementedDetails = MapToProblemDetails(content);
                    throw new HttpResponseNotImplementedException(httpResponseMessage, NotImplementedDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.NotImplemented:
                    throw new HttpResponseNotImplementedException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.RequestUriTooLong:
                    ValidationProblemDetails RequestUriTooLongDetails = MapToProblemDetails(content);
                    throw new HttpResponseRequestUriTooLongException(httpResponseMessage, RequestUriTooLongDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.RequestUriTooLong:
                    throw new HttpResponseRequestUriTooLongException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.RequestTimeout:
                    ValidationProblemDetails RequestTimeoutDetails = MapToProblemDetails(content);
                    throw new HttpResponseRequestTimeoutException(httpResponseMessage, RequestTimeoutDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.RequestTimeout:
                    throw new HttpResponseRequestTimeoutException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.ServiceUnavailable:
                    ValidationProblemDetails ServiceUnavailableDetails = MapToProblemDetails(content);
                    throw new HttpResponseServiceUnavailableException(httpResponseMessage, ServiceUnavailableDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.ServiceUnavailable:
                    throw new HttpResponseServiceUnavailableException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.Gone:
                    ValidationProblemDetails GoneDetails = MapToProblemDetails(content);
                    throw new HttpResponseGoneException(httpResponseMessage, GoneDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.Gone:
                    throw new HttpResponseGoneException(httpResponseMessage, content);

                case true when httpResponseMessage.StatusCode == HttpStatusCode.GatewayTimeout:
                    ValidationProblemDetails gatewayDetails = MapToProblemDetails(content);
                    throw new HttpResponseGatewayTimeoutException(httpResponseMessage, gatewayDetails);

                case false when httpResponseMessage.StatusCode == HttpStatusCode.GatewayTimeout:
                    throw new HttpResponseGatewayTimeoutException(httpResponseMessage, content);
            }
        }
        public static bool OKWithNoContent(HttpResponseMessage httpResponseMessage) =>
            httpResponseMessage.Content.Headers.Contains("Content-Type") == false
            && httpResponseMessage.StatusCode == HttpStatusCode.OK;

        private static bool NotFoundWithNoContent(HttpResponseMessage httpResponseMessage) =>
            httpResponseMessage.Content.Headers.Contains("Content-Type") == false
            && httpResponseMessage.StatusCode == HttpStatusCode.NotFound;

        private static ValidationProblemDetails MapToProblemDetails(string content) =>
            JsonConvert.DeserializeObject<ValidationProblemDetails>(content);

        private static bool IsProblemDetail(string content) =>
            content.ToLower().Contains("\"title\":") && content.ToLower().Contains("\"type\":");
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseException : Xeption
    {
        public HttpResponseException() { }

        public HttpResponseException(HttpResponseMessage? httpResponseMessage, string? message)
            : base(message) => this.HttpResponseMessage = httpResponseMessage;

        public HttpResponseMessage? HttpResponseMessage { get; private set; }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseBadRequestException : HttpResponseException
    {
        public HttpResponseBadRequestException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseBadRequestException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseBadRequestException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseUnauthorizedException : HttpResponseException
    {
        public HttpResponseUnauthorizedException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseUnauthorizedException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseUnauthorizedException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseUrlNotFoundException : HttpResponseException
    {
        public HttpResponseUrlNotFoundException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseUrlNotFoundException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseNotFoundException : HttpResponseException
    {
        public HttpResponseNotFoundException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseNotFoundException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseNotFoundException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseInternalServerErrorException : HttpResponseException
    {
        public HttpResponseInternalServerErrorException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseInternalServerErrorException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseInternalServerErrorException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseConflictException : HttpResponseException
    {
        public HttpResponseConflictException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseConflictException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseConflictException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseForbiddenException : HttpResponseException
    {
        public HttpResponseForbiddenException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseForbiddenException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseForbiddenException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseMethodNotAllowedException : HttpResponseException
    {
        public HttpResponseMethodNotAllowedException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseMethodNotAllowedException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseMethodNotAllowedException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseNotAcceptableException : HttpResponseException
    {
        public HttpResponseNotAcceptableException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseNotAcceptableException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseNotAcceptableException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseNotImplementedException : HttpResponseException
    {
        public HttpResponseNotImplementedException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseNotImplementedException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseNotImplementedException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseRequestUriTooLongException : HttpResponseException
    {
        public HttpResponseRequestUriTooLongException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseRequestUriTooLongException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseRequestUriTooLongException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseRequestTimeoutException : HttpResponseException
    {
        public HttpResponseRequestTimeoutException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseRequestTimeoutException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseRequestTimeoutException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseServiceUnavailableException : HttpResponseException
    {
        public HttpResponseServiceUnavailableException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseServiceUnavailableException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseServiceUnavailableException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseGoneException : HttpResponseException
    {
        public HttpResponseGoneException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseGoneException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseGoneException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class HttpResponseGatewayTimeoutException : HttpResponseException
    {
        public HttpResponseGatewayTimeoutException()
            : base(httpResponseMessage: default, message: default) { }

        public HttpResponseGatewayTimeoutException(HttpResponseMessage responseMessage, string message)
            : base(responseMessage, message) { }

        public HttpResponseGatewayTimeoutException(
            HttpResponseMessage responseMessage,
            ValidationProblemDetails problemDetails) : base(responseMessage, problemDetails.Title)
        {
            this.AddData((IDictionary)problemDetails.Errors);
        }
    }

    [ExcludeFromCodeCoverage]
    public class Xeption : Exception
    {
        public Xeption() : base() { }

        public Xeption(string? message) : base(message) { }

        public Xeption(string message, Exception innerException)
            : base(message, innerException)
        { }

        public Xeption(Exception innerException, IDictionary data)
            : base(innerException.Message, innerException)
        {
            this.AddData(data);
        }

        public Xeption(string message, Exception innerException, IDictionary data)
            : base(message, innerException)
        {
            this.AddData(data);
        }

        public void UpsertDataList(string key, string value)
        {
            if (this.Data.Contains(key))
            {
                (this.Data[key] as List<string>)?.Add(value);
            }
            else
            {
                this.Data.Add(key, new List<string> { value });
            }
        }

        public void ThrowIfContainsErrors()
        {
            if (this.Data.Count > 0)
            {
                throw this;
            }
        }

        public void AddData(IDictionary dictionary)
        {
            if (dictionary != null)
            {
                foreach (DictionaryEntry item in dictionary)
                {
                    this.Data.Add(item.Key, item.Value);
                }
            }
        }
    }
}