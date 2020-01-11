using Surging.Core.CPlatform.Exceptions;
using Surging.Core.CPlatform.Messages;
using Surging.Core.ProxyGenerator.Implementation;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Surging.Core.ProxyGenerator.Interceptors.Implementation
{
    public class ActionInvocation : AbstractInvocation
    {
        protected ActionInvocation(
             IDictionary<string, object> arguments,
           string serviceId,
            string[] cacheKey,
            List<Attribute> attributes,
            Type returnType,
            object proxy
            ) : base(arguments, serviceId, cacheKey, attributes, returnType, proxy)
        {
        }

        public override async Task Proceed()
        {
            try
            {
                if (_returnValue == null)
                {
                    var proceedResult = await (Proxy as ServiceProxyBase).CallInvoke(this);
                    _returnValue = proceedResult;
                    _remoteInvokeResultMessage = new RemoteInvokeResultMessage()
                    {
                        Result = proceedResult,
                    };
                }
            }
            catch (Exception ex)
            {
                _returnValue = null;
                _remoteInvokeResultMessage = new RemoteInvokeResultMessage()
                {
                    ExceptionMessage = ex.GetExceptionMessage(),
                    StatusCode = ex.GetGetExceptionStatusCode()
                };
            }
        }
    }
}