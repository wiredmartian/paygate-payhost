import { Component } from '@angular/core';
import {NgForm} from '@angular/forms';
import {NewCardModel} from '../../models/payment.models';
import {PaymentService} from '../../api/payment.service';
import Socket = SocketIOClient.Socket;
import {DomSanitizer} from '@angular/platform-browser';

@Component({
  selector: 'app-pay',
  templateUrl: './pay.component.html',
  styleUrls: ['./pay.component.scss']
})
export class PayComponent {
  model = {} as NewCardModel;
  socket: Socket;
  Secure3DHTML: any;
  payRequestId: string;

  constructor(
    private paySvc: PaymentService,
    private sanitizer: DomSanitizer,
  ) { }

  makePayment(form: NgForm) {
    if (form.submitted && form.valid) {
      // form.value == this.model
      this.paySvc.tokenizeCard(form.value).toPromise()
        .then((res) => {
          /**
           * {"Status":
           * {
           * "TransactionId":"292899202",
           * "Reference":"bd41987e-0127-47d4-8dea-217da036d819",
           * "AcquirerCode":"00",
           * "StatusName":"Completed",
           * "AuthCode":"M95HBZ",
           * "PayRequestId":"9EB11432-A807-4C78-A73C-959731F67EEF",
           * "VaultId":"3a7615eb-2af6-4a29-98cd-dbc67c0e4fb2",
           * "PayVaultData":
           *      [
           *      {"name":"cardNumber","value":"xxxxxxxxxxxx0015"},
           *      {"name":"expDate","value":"112023"}
           *      ],
           * "TransactionStatusCode":"1",
           * "TransactionStatusDescription":"Approved",
           * "ResultCode":"990017",
           * "ResultDescription":"Auth Done",
           * "Currency":"ZAR",
           * "Amount":"200","RiskIndicator":"AP",
           * "PaymentType":{"Method":"CC","Detail":"MasterCard"}}
           * }
           */
          if (res.completed) {
            // payment was completed
          } else {
            console.log(res);
            // probably 3D secure
            this.Secure3DHTML = this.sanitizer.bypassSecurityTrustResourceUrl(`data:text/html,${res.secure3DHtml}`);
            this.payRequestId = res.payRequestId;
            this.connectToSocket(res.payRequestId);
          }
        }).catch(err => {
          console.log(err);
      });
    }
  }

  connectToSocket(payId: string) {
    this.socket = this.paySvc.openSocketConnection(payId);
    this.socket.on('message', (e: any) => {
      console.log('connected to socket');
      // this.socketMessages = e;
    });
    this.socket.on('joined', (content: any) => {
      console.log(content);
      // this.socketMessages = content;
    });
    this.socket.on('complete', async (payload: any) => {
      this.Secure3DHTML = null;
      this.completeFollowUp();
    });
  }

  completeFollowUp() {
    this.paySvc.queryTransaction(this.payRequestId).toPromise()
      .then(async (res) => {
        console.log(res);
    }, async error => {
      console.error(error);
    });
  }
}
