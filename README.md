## How to run the code
You can open the code up in visual studio 2022. Other versions will probably work too. I believe the code will compile, but you won't be able to run any API requests as it requires environment variables containing client ids and client secret keys that I cannot provide. 

The purpose of sharing this code is to show that I have experience working with APIs. I other code samples I could share in an interview setting (**subtle hint**) that I couldn't share on a public repository on GITHUB due to sensitive information.

## Background about this code sample
This some code that I wrote to integrate with the Carton Cloud API (https://api-docs.cartoncloud.com/). This code snippet belongs in a larger repository containing intergrations with other APIs. 
I have extracted just the code that relates to Carton Cloud for demonstration purposes

The story behind this code is that a client stores stock in some warehouses that use the Carton Cloud system to track Inventory, process Picks and process Receipts. The client was manually extracting stock on hand reports out of Carton Cloud's user portal 
to track their inventory at the warehouses. They were also manually keying in sales orders whenever they needed to release stock from the warehouses to be delivered to their customers. 

I was able to help the client by using the Carton Cloud API to fetch the stock on hand reports automatically every night, and sync the stock with the order management system (OMS) I built for them. I was also able to set up a system for whenever the client released a 
sales order inside of our OMS, it would send an API request to carton cloud with the details of the sales order. This saved the client from having to key the order in twice, once in their OMS, and once in Carton Cloud's user portal.

Finally, I was also able to take advantage of the Carton Cloud's webhooks to provide real time updates whenever the status of a sales order changed. (when the warehouse started to pick the order, when it finished picking, and when the order was picked up for delivery 
by the transporter). 

## Project Structure
This is a project containing an Azure function app written in C#.

`/Reckon API` folder. Originally, this project was just building an API to interact with Reckon, however, it quickly supported multiple integrations. I have just extracted the Carton Cloud related code.

`SPL API.sln` the visual studio solution file. Ignore the name.

`/Reckon API/CartonCloudAPI.cs` the file containing the function app endpoints for receiving the API requests and handling request logic. 

`/Reckon API/Auth.cs` some code used to authenticate the Carton Cloud webhooks.

`/Reckon API/AppUtilities.cs` some utility methods.

`/Reckon API/Interfaces/CartonCloudInterface.cs` the file containing all the functions used to directy interact with the Carton Cloud API.

`/Reckon API/Interfaces/ZohoInterface.cs` the file containing all the functions used to interact with the zoho creator server. This is the system used to create the order management system for the client. Its basicaly a database with some backend functions. 

## What i learnt from this project
In terms of an API integration, this was pretty simple. However, the key learning outcomes were to do with building the system to track the inventory for the client. This was just one aspect of the problem. But the customer has multiple warehouses all across
Australia. The different warehouse have different software powering them. Carton Cloud was the best to work with because it has an API that just works. The other warehouses don't. Instead they send files via email to the client. These files are sometimes in CSV format
(yay), but sometimes were an .rtf file (yuck). The real challenge for this project is getting all this information with varying levels of accuracy and detail, and using all the information to power the OMS for the client. It hasn't been easy, but its been really
rewarding now that the client is using everything i've built for them. 
