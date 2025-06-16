using System;
using System.Threading.Tasks;
using Grpc.Core;

namespace Quaestor.ProcessAdministration
{
	public class ProcessAdministrationGrpcImpl
		: ProcessAdministrationGrpc.ProcessAdministrationGrpcBase
	{
		public ProcessAdministrationGrpcImpl()
		{
			RequestAdmin = new RequestAdmin();
		}

		public IRequestAdmin RequestAdmin { get; }

		#region Overrides of ProcessAdministrationGrpcBase

		public override Task<CancelResponse> Cancel(CancelRequest request,
		                                            ServerCallContext context)
		{
			if (RequestAdmin == null)
			{
				throw new InvalidOperationException("Request admin has not been initialized.");
			}

			RequestAdmin.CancelRequest(request.UserName, request.Environment);

			return Task.FromResult(new CancelResponse
			{
				Success = true
			});
		}

		#endregion
	}
}
