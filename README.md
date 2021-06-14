# Paygate PayHost

This is a simple implementation of online payments using Paygate's PayHost.

Docs can be found on the official website (https://docs.paygate.co.za/#payhost)


## Get Started

To get started, you need to have <b>Angular 12+</b> installed.

You also need <b>.NET Core 3.1</b> installed.

## Running the app

Change the redirect urls in `/Templates/SinglePaymentRequest.xml` to live (not localhost) urls.

The `NotifyUrl` has to be a `POST` endpoint, where Paygate will post the results of a payment.

```xml
<Redirect>
    <NotifyUrl>https://sockets.diggipiggy.xyz/payment/complete</NotifyUrl>
    <ReturnUrl>https://sockets.diggipiggy.xyz/payment/complete</ReturnUrl>
</Redirect>
```

In this case, I used a sockets so that the UI will be able to catch the response from that endpoint.

You can clone this socket service (https://github.com/wiredmartian/socket-service), and deploy it.

Use the url of where it sits as yoyr ReturnUrl and NotifyUrl like above.

<br/>

And finally, to run the API (root of this project):

`$ dotnet run` - the API runs on port :5000 and securely on :5001


To run the client:

`$ cd payhost-client` - move into the client directory

`$ npm i` - install dependencies

`$ ng serve` - to run app (runs on port :4200)


## Testing

Head over to (https://docs.paygate.co.za/#testing-2) for testing Bank Cards.

#### Approved Transactions

RESULT_CODE = 990017; TRANSACTION_STATUS = 1

<table>
    <thead>
        <td>Card Brand</td>
        <td>Card Number</td>
        <td>Risk Indicator</td>
    </thead>
    <tbody>
        <tr>
            <td>Visa</td>
            <td>4000000000000002</td>
            <td>Authenticated (AX) *</td>
        </tr>
        <tr>
            <td>MasterCard</td>
            <td>5200000000000015</td>
            <td>Authenticated (AX) *</td>
        </tr>
    </tbody>
</table>

#### Insufficient Funds Transactions

RESULT_CODE = 900003; TRANSACTION_STATUS = 2

<table>
    <thead>
        <td>Card Brand</td>
        <td>Card Number</td>
        <td>Risk Indicator</td>
    </thead>
    <tbody>
        <tr>
            <td>Visa</td>
            <td>4000000000000028</td>
            <td>Not Authenticated (NX)</td>
        </tr>
        <tr>
            <td>MasterCard</td>
            <td>5200000000000023</td>
            <td>Not Authenticated (NX) *</td>
        </tr>
    </tbody>
</table>

#### Declined Transactions

RESULT_CODE = 900007; TRANSACTION_STATUS = 2

<table>
    <thead>
        <td>Card Brand</td>
        <td>Card Number</td>
        <td>Risk Indicator</td>
    </thead>
    <tbody>
        <tr>
            <td>Visa</td>
            <td>4000000000000036</td>
            <td>Authenticated (AX) *</td>
        </tr>
        <tr>
            <td>MasterCard</td>
            <td>5200000000000049</td>
            <td>Authenticated (AX) *</td>
        </tr>
    </tbody>
</table>


